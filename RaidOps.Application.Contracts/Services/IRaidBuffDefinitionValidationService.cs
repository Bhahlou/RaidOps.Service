using RaidOps.Application.Contracts.Raids.Buffs;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Validates raid buff definitions against the reference data they must point at (spells on the
/// expansion, classes, specs) — the one place both the upsert/import and the edit-by-ID paths check
/// a definition before writing it.
/// </summary>
public interface IRaidBuffDefinitionValidationService
{
    /// <summary>
    /// Returns one human-readable message per problem found in <paramref name="definitions"/> for
    /// <paramref name="expansionId"/>; empty when everything is valid.
    /// </summary>
    Task<List<string>> ValidateAsync(int expansionId, IReadOnlyList<RaidBuffDefinitionInput> definitions, CancellationToken cancellationToken = default);
}
