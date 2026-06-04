using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Implementations.Characters.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

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
    ISpecResolverService specResolver)
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
            return Result<CommandResponse>.Fail(ResponseDetail.NotFound);

        var bnetAccount = await bnetAccountRepository.GetByDiscordIdAsync(command.UserDiscordId, cancellationToken);

        if (bnetAccount is not null)
        {
            try
            {
                var profileNamespace = "profile" + character.Branch.BnetNamespacePrefix["dynamic".Length..] + "-" + bnetAccount.Region;
                var realmSlug = character.Realm.Slug;
                var name = character.Name;

                var detailTask = bnetApiService.GetCharacterAsync(bnetAccount.AccessToken, bnetAccount.Region, profileNamespace, realmSlug, name, cancellationToken);
                var mediaTask  = bnetApiService.GetCharacterMediaAsync(bnetAccount.AccessToken, bnetAccount.Region, profileNamespace, realmSlug, name, cancellationToken);
                var specsTask  = bnetApiService.GetCharacterSpecializationsAsync(bnetAccount.AccessToken, bnetAccount.Region, profileNamespace, realmSlug, name, cancellationToken);

                await Task.WhenAll(detailTask, mediaTask, specsTask);

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
            catch (HttpRequestException) { }
        }

        // Reload to get fresh navigations (specs with icons, updated fields) for DTO mapping.
        var refreshed = (await characterRepository.GetByUserWithDetailsAsync(
            command.UserDiscordId, activeOnly: true, cancellationToken))
            .First(c => c.Id == command.CharacterId);

        return Result<CommandResponse>.Ok(new CommandResponse("Character resynced successfully.", MapToDto(refreshed)));
    }

    private static CharacterDto MapToDto(Character c)
    {
        var activeState = c.ExpansionStates.FirstOrDefault(s => s.IsActive)
                       ?? c.ExpansionStates.OrderByDescending(s => s.Level).FirstOrDefault();

        return new CharacterDto
        {
            Id         = c.Id,
            Name       = c.Name,
            ClassId    = c.ClassId,
            ClassName  = c.Class.Name,
            ClassColor = "#" + c.Class.Color,
            RaceId     = c.RaceId,
            RaceName   = c.Race.Name,
            Faction    = c.Faction.ToString().ToUpperInvariant(),
            BranchName = c.Branch.Name,
            RealmName  = c.Realm.Name,
            RealmSlug  = c.Realm.Slug,
            Level      = activeState?.Level ?? 0,
            ItemLevel  = activeState?.ItemLevel,
            AvatarUrl  = c.AvatarUrl,
            GuildName  = activeState?.GuildName,
            Specs      = (activeState?.Specs ?? [])
                .OrderByDescending(s => s.IsMain)
                .Select(s => new CharacterSpecDto
                {
                    SpecId  = s.SpecId,
                    Name    = s.Spec.Name,
                    IconUrl = s.Spec.IconUrl,
                    IsMain  = s.IsMain,
                })
                .ToList(),
        };
    }
}
