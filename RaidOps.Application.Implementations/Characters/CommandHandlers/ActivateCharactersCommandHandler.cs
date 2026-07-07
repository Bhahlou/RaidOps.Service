using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

/// <summary>
/// Handles <see cref="ActivateCharactersCommand"/> by marking the given characters as active
/// in RaidOps and enriching them with data pulled from the Battle.net API
/// (avatar URL, guild name, active specs).
/// BNet API calls are made in parallel; DB writes are sequential to respect EF Core's
/// single-threaded DbContext constraint.
/// If the BNet API is unreachable for a given character, activation still proceeds
/// without enrichment.
/// </summary>
public class ActivateCharactersCommandHandler(
    ICharacterRepository characterRepository,
    IBnetAccountRepository bnetAccountRepository,
    IBnetApiService bnetApiService,
    ISpecResolverService specResolver,
    ILogger<ActivateCharactersCommandHandler> logger)
    : ICommandHandlerAsync<ActivateCharactersCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        ActivateCharactersCommand command,
        CancellationToken cancellationToken = default)
    {
        var bnetAccount = await bnetAccountRepository.GetByDiscordIdAsync(command.UserDiscordId, cancellationToken);
        var characters = (await characterRepository.GetByIdsWithDetailsAsync(
            command.CharacterIds, command.UserDiscordId, cancellationToken)).ToList();

        string? appToken = null;
        if (bnetAccount is not null)
        {
            try { appToken = await bnetApiService.GetAppTokenAsync(bnetAccount.Region, cancellationToken); }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex,
                    "Activation enrichment skipped for discord user {DiscordId}: could not obtain BNet app token for region {Region}",
                    command.UserDiscordId, bnetAccount.Region);
            }
        }

        // Fetch BNet data for all characters in parallel (pure HTTP, no DbContext).
        var fetchTasks = characters.Select(c => FetchEnrichmentAsync(c, appToken, bnetAccount?.Region, cancellationToken));
        var enrichments = await Task.WhenAll(fetchTasks);

        // Persist sequentially — DbContext is not thread-safe.
        for (var i = 0; i < characters.Count; i++)
            await PersistEnrichmentAsync(characters[i], enrichments[i], cancellationToken);

        await characterRepository.ActivateAsync(command.CharacterIds, command.UserDiscordId, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Activated {CharacterCount} character(s) for discord user {DiscordId} ({EnrichedCount} enriched from BNet)",
                characters.Count, command.UserDiscordId, enrichments.Count(e => e is not null));
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Characters activated successfully."));
    }

    private async Task<CharacterEnrichment?> FetchEnrichmentAsync(
        Character character,
        string? accessToken,
        string? region,
        CancellationToken cancellationToken)
    {
        if (accessToken is null || region is null) return null;

        try
        {
            var profileNamespace = "profile" + character.Branch.BnetNamespacePrefix["dynamic".Length..] + "-" + region;
            var realmSlug = character.Realm.Slug;
            var name = character.Name;

            var detailTask = bnetApiService.GetCharacterAsync(accessToken, region, profileNamespace, realmSlug, name, cancellationToken);
            var mediaTask  = bnetApiService.GetCharacterMediaAsync(accessToken, region, profileNamespace, realmSlug, name, cancellationToken);
            var specsTask  = bnetApiService.GetCharacterSpecializationsAsync(accessToken, region, profileNamespace, realmSlug, name, cancellationToken);

            await Task.WhenAll(detailTask, mediaTask, specsTask);

            return new CharacterEnrichment(detailTask.Result, mediaTask.Result, specsTask.Result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex,
                "Activation enrichment skipped for character {CharacterId} ({CharacterName}): BNet API call failed",
                character.Id, character.Name);
            return null;
        }
    }

    private async Task PersistEnrichmentAsync(
        Character character,
        CharacterEnrichment? enrichment,
        CancellationToken cancellationToken)
    {
        if (enrichment is null) return;

        character.AvatarUrl = enrichment.Media.Assets.FirstOrDefault(a => a.Key == "avatar")?.Value;
        await characterRepository.UpsertAsync(character, cancellationToken);

        var expansionId  = character.Branch.CurrentExpansionId;
        var existingState = character.ExpansionStates.FirstOrDefault(s => s.ExpansionId == expansionId);

        var state = existingState ?? new CharacterExpansionState
        {
            CharacterId = character.Id,
            ExpansionId = expansionId,
        };

        state.Level     = enrichment.Detail.Level;
        state.ItemLevel = enrichment.Detail.EquippedItemLevel > 0 ? enrichment.Detail.EquippedItemLevel : null;
        state.IsActive  = true;
        state.GuildName = enrichment.Detail.Guild?.Name;
        state.Specs     = await specResolver.ResolveAsync(enrichment.Specs, character.ClassId, state, cancellationToken);

        await characterRepository.UpsertExpansionStateAsync(state, cancellationToken);
    }

    private sealed record CharacterEnrichment(
        BnetCharacterDetailResponse Detail,
        BnetCharacterMediaResponse Media,
        BnetCharacterSpecializationsResponse Specs);
}
