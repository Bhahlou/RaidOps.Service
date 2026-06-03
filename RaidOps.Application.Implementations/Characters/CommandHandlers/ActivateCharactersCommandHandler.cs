using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
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
    IBnetApiService bnetApiService)
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

        // Fetch BNet data for all characters in parallel (pure HTTP, no DbContext).
        var fetchTasks = characters.Select(c => FetchEnrichmentAsync(c, bnetAccount?.AccessToken, bnetAccount?.Region, cancellationToken));
        var enrichments = await Task.WhenAll(fetchTasks);

        // Persist sequentially — DbContext is not thread-safe.
        for (var i = 0; i < characters.Count; i++)
            await PersistEnrichmentAsync(characters[i], enrichments[i], cancellationToken);

        await characterRepository.ActivateAsync(command.CharacterIds, command.UserDiscordId, cancellationToken);

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
        catch (HttpRequestException)
        {
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
        state.Specs     = await ResolveSpecsAsync(enrichment.Specs, character.ClassId, state, cancellationToken);

        await characterRepository.UpsertExpansionStateAsync(state, cancellationToken);
    }

    private Task<ICollection<CharacterSpec>> ResolveSpecsAsync(
        BnetCharacterSpecializationsResponse specsResponse,
        int classId,
        CharacterExpansionState state,
        CancellationToken cancellationToken)
    {
        return specsResponse.ActiveSpecialization is not null
            ? ResolveMopSpecsAsync(specsResponse, state, cancellationToken)
            : ResolveClassicSpecsAsync(specsResponse, classId, state, cancellationToken);
    }

    /// <summary>
    /// MoP / Retail: active spec from <c>active_specialization.id</c>,
    /// offspec from the first other entry in <c>specializations</c>.
    /// </summary>
    private async Task<ICollection<CharacterSpec>> ResolveMopSpecsAsync(
        BnetCharacterSpecializationsResponse specsResponse,
        CharacterExpansionState state,
        CancellationToken cancellationToken)
    {
        var activeId = specsResponse.ActiveSpecialization!.Id;
        var result = new List<CharacterSpec>();

        var mainSpec = await characterRepository.GetSpecByIdAsync(activeId, cancellationToken);
        if (mainSpec is not null)
            result.Add(new CharacterSpec { CharacterExpansionStateId = state.Id, SpecId = mainSpec.Id, IsMain = true });

        var offspecEntry = specsResponse.Specializations
            .FirstOrDefault(s => s.Specialization.Id != activeId);
        if (offspecEntry is not null)
        {
            var offSpec = await characterRepository.GetSpecByIdAsync(offspecEntry.Specialization.Id, cancellationToken);
            if (offSpec is not null)
                result.Add(new CharacterSpec { CharacterExpansionStateId = state.Id, SpecId = offSpec.Id, IsMain = false });
        }

        return result;
    }

    /// <summary>
    /// Classic / TBC: top 2 talent trees by spent points from the active loadout.
    /// </summary>
    private async Task<ICollection<CharacterSpec>> ResolveClassicSpecsAsync(
        BnetCharacterSpecializationsResponse specsResponse,
        int classId,
        CharacterExpansionState state,
        CancellationToken cancellationToken)
    {
        var activeGroup = specsResponse.SpecializationGroups.FirstOrDefault(g => g.IsActive);
        if (activeGroup is null) return [];

        var topTrees = activeGroup.Specializations
            .Where(t => t.SpentPoints > 0)
            .OrderByDescending(t => t.SpentPoints)
            .Take(2)
            .ToList();

        var result = new List<CharacterSpec>();
        for (var i = 0; i < topTrees.Count; i++)
        {
            var spec = await characterRepository.GetSpecByNameAndClassAsync(topTrees[i].SpecializationName, classId, cancellationToken);
            if (spec is null) continue;

            result.Add(new CharacterSpec { CharacterExpansionStateId = state.Id, SpecId = spec.Id, IsMain = i == 0 });
        }

        return result;
    }

    private sealed record CharacterEnrichment(
        BnetCharacterDetailResponse Detail,
        BnetCharacterMediaResponse Media,
        BnetCharacterSpecializationsResponse Specs);
}
