using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.Services;

/// <summary>
/// Resolves main and offspec from the Battle.net API response.
///
/// Classic (Vanilla → Cata): each dual-spec loadout is a <c>specialization_group</c>.
///   The dominant talent tree (most spent points) determines the spec.
///   The active group = main spec; the inactive group = offspec.
///
/// Modern (MoP+): <c>active_specialization</c> is present and used directly.
///
/// Extend this service with new <c>Resolve*</c> methods as expansion-specific
/// rules evolve (e.g. Dragonflight hero talents).
/// </summary>
public class SpecResolverService(ICharacterRepository characterRepository) : ISpecResolverService
{
    /// <inheritdoc/>
    public Task<ICollection<CharacterSpec>> ResolveAsync(
        BnetCharacterSpecializationsResponse specsResponse,
        int classId,
        CharacterExpansionState state,
        CancellationToken cancellationToken = default)
    {
        return specsResponse.ActiveSpecialization is not null
            ? ResolveModernAsync(specsResponse, state, cancellationToken)
            : ResolveClassicAsync(specsResponse, classId, state, cancellationToken);
    }

    private async Task<ICollection<CharacterSpec>> ResolveModernAsync(
        BnetCharacterSpecializationsResponse specsResponse,
        CharacterExpansionState state,
        CancellationToken cancellationToken)
    {
        var activeId = specsResponse.ActiveSpecialization!.Id;
        var result = new List<CharacterSpec>();

        var mainSpec = await characterRepository.GetSpecByIdAsync(activeId, cancellationToken);
        if (mainSpec is not null)
            result.Add(new CharacterSpec { CharacterExpansionStateId = state.Id, SpecId = mainSpec.Id, IsMain = true });

        var offspecEntry = specsResponse.Specializations.FirstOrDefault(s => s.Specialization.Id != activeId);
        if (offspecEntry is not null)
        {
            var offSpec = await characterRepository.GetSpecByIdAsync(offspecEntry.Specialization.Id, cancellationToken);
            if (offSpec is not null && result.All(r => r.SpecId != offSpec.Id))
                result.Add(new CharacterSpec { CharacterExpansionStateId = state.Id, SpecId = offSpec.Id, IsMain = false });
        }

        return result;
    }

    private async Task<ICollection<CharacterSpec>> ResolveClassicAsync(
        BnetCharacterSpecializationsResponse specsResponse,
        int classId,
        CharacterExpansionState state,
        CancellationToken cancellationToken)
    {
        var activeGroup   = specsResponse.SpecializationGroups.FirstOrDefault(g => g.IsActive);
        var inactiveGroup = specsResponse.SpecializationGroups.FirstOrDefault(g => !g.IsActive);

        var result = new List<CharacterSpec>();

        var mainTree = activeGroup?.Specializations.MaxBy(t => t.SpentPoints);
        if (mainTree is not null)
        {
            var mainSpec = await characterRepository.GetSpecByNameAndClassAsync(ResolveClassicSpecName(mainTree), classId, cancellationToken);
            if (mainSpec is not null)
                result.Add(new CharacterSpec { CharacterExpansionStateId = state.Id, SpecId = mainSpec.Id, IsMain = true });
        }

        var offTree = inactiveGroup?.Specializations.MaxBy(t => t.SpentPoints);
        if (offTree is not null)
        {
            var offSpec = await characterRepository.GetSpecByNameAndClassAsync(ResolveClassicSpecName(offTree), classId, cancellationToken);
            // No dedup: same-spec dual-spec is valid in Classic (e.g. Ret/Ret with different talent builds).
            if (offSpec is not null)
                result.Add(new CharacterSpec { CharacterExpansionStateId = state.Id, SpecId = offSpec.Id, IsMain = false });
        }

        return result;
    }

    // Feral Combat (Vanilla → Cata) maps to Feral for both DPS and tank builds —
    // Guardian only became a distinct spec in MoP. Role distinction is handled
    // via GuildMembership.assigned_specs, not inferred from talents.
    private static string ResolveClassicSpecName(BnetSpecializationTreeDto tree) => tree.SpecializationName;
}
