using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Resolves a character's main and offspec from a Battle.net specialization response.
/// Handles Classic (talent trees) and Modern (MoP+) spec formats.
/// </summary>
public interface ISpecResolverService
{
    /// <summary>
    /// Returns the resolved <see cref="BnetCharacterSpec"/> collection (main + offspec)
    /// to persist on the given <paramref name="state"/>.
    /// </summary>
    Task<ICollection<BnetCharacterSpec>> ResolveAsync(
        BnetCharacterSpecializationsResponse specsResponse,
        int classId,
        CharacterExpansionState state,
        CancellationToken cancellationToken = default);
}
