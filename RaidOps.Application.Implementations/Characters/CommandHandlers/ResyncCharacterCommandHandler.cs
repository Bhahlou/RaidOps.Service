using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

/// <summary>
/// Handles <see cref="ResyncCharacterCommand"/> by re-fetching the character's data
/// from the Battle.net API (avatar, level, item level, guild, specs) and returning
/// the updated <see cref="CharacterDto"/>.
/// If the BNet API is unreachable, the character is returned as-is without enrichment.
/// </summary>
public class ResyncCharacterCommandHandler(
    ICharacterRepository characterRepository,
    IBnetAccountRepository bnetAccountRepository,
    IBnetApiService bnetApiService,
    ISpecResolverService specResolver,
    ILogger<ResyncCharacterCommandHandler> logger)
    : ICommandHandlerAsync<ResyncCharacterCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        ResyncCharacterCommand command,
        CancellationToken cancellationToken = default)
    {
        var characters = await characterRepository.GetByUserWithDetailsAsync(
            command.UserDiscordId, activeOnly: true, cancellationToken);
        var character = characters.FirstOrDefault(c => c.Id == command.CharacterId);

        if (character is null)
        {
            logger.LogWarning(
                "Resync failed for discord user {DiscordId}: character {CharacterId} not found",
                command.UserDiscordId, command.CharacterId);
            return Result<CommandResponse>.Fail(ResponseDetail.NotFound);
        }

        var bnetAccount = await bnetAccountRepository.GetByDiscordIdAsync(command.UserDiscordId, cancellationToken);

        if (bnetAccount is not null)
        {
            var profileNamespace = "profile" + character.Branch.BnetNamespacePrefix["dynamic".Length..] + "-" + bnetAccount.Region;
            var realmSlug = character.Realm.Slug;
            var name = character.Name;

            logger.LogInformation(
                "Resyncing character {CharacterId} ({CharacterName}) for discord user {DiscordId}, branch {BranchId}, namespace {Namespace}, realm {RealmSlug}",
                character.Id, name, command.UserDiscordId, character.BranchId, profileNamespace, realmSlug);

            string appToken;
            try
            {
                appToken = await bnetApiService.GetAppTokenAsync(bnetAccount.Region, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex,
                    "Resync failed for character {CharacterId} ({CharacterName}), discord user {DiscordId}: could not obtain BNet app token for region {Region}",
                    character.Id, name, command.UserDiscordId, bnetAccount.Region);
                return Result<CommandResponse>.Fail(ResponseDetail.BnetApiError);
            }

            var detailTask = bnetApiService.GetCharacterAsync(appToken, bnetAccount.Region, profileNamespace, realmSlug, name, cancellationToken);
            var mediaTask  = bnetApiService.GetCharacterMediaAsync(appToken, bnetAccount.Region, profileNamespace, realmSlug, name, cancellationToken);
            var specsTask  = bnetApiService.GetCharacterSpecializationsAsync(appToken, bnetAccount.Region, profileNamespace, realmSlug, name, cancellationToken);

            try
            {
                await Task.WhenAll(detailTask, mediaTask, specsTask);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex,
                    "Resync failed for character {CharacterId} ({CharacterName}), discord user {DiscordId}: BNet API call failed for namespace {Namespace}, realm {RealmSlug}",
                    character.Id, name, command.UserDiscordId, profileNamespace, realmSlug);
                return Result<CommandResponse>.Fail(ResponseDetail.BnetApiError);
            }

            character.AvatarUrl = mediaTask.Result.Assets.FirstOrDefault(a => a.Key == "avatar")?.Value;
            await characterRepository.UpsertAsync(character, cancellationToken);

            var expansionId   = character.Branch.CurrentExpansionId;
            var existingState = character.ExpansionStates.FirstOrDefault(s => s.ExpansionId == expansionId);

            var state = existingState ?? new CharacterExpansionState
            {
                CharacterId = character.Id,
                ExpansionId = expansionId,
            };

            state.Level     = detailTask.Result.Level;
            state.ItemLevel = detailTask.Result.EquippedItemLevel > 0 ? detailTask.Result.EquippedItemLevel : null;
            state.IsActive  = true;
            state.GuildName = detailTask.Result.Guild?.Name;
            state.Specs     = await specResolver.ResolveAsync(specsTask.Result, character.ClassId, state, cancellationToken);

            await characterRepository.UpsertExpansionStateAsync(state, cancellationToken);
        }

        // Reload to get fresh navigations (specs with icons, updated fields) for DTO mapping.
        var refreshed = (await characterRepository.GetByUserWithDetailsAsync(
            command.UserDiscordId, activeOnly: true, cancellationToken))
            .First(c => c.Id == command.CharacterId);

        var dto = CharacterMapper.ToDto(refreshed);

        return Result<CommandResponse>.Ok(new CommandResponse("Character resynced successfully.", dto));
    }
}
