using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="RaidBuffDefinition"/> persistence — the curated raid buff/debuff list, kept per expansion.</summary>
public interface IRaidBuffDefinitionsRepository
{
    /// <summary>
    /// Returns every definition of <paramref name="expansionId"/> ordered by
    /// <see cref="RaidBuffDefinition.SortOrder"/>, with its sources and its spell's
    /// <see cref="SpellAvailability"/> on that expansion (the only availability loaded) so callers can
    /// resolve name and icon.
    /// </summary>
    Task<List<RaidBuffDefinition>> GetForExpansionAsync(int expansionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="incoming"/> to <paramref name="expansionId"/> in a single save: an entry
    /// whose spell already has a definition overwrites its fields and replaces its sources, any other
    /// entry is inserted. When <paramref name="pruneMissing"/> is set, existing definitions whose spell
    /// isn't in <paramref name="incoming"/> are deleted.
    /// </summary>
    /// <returns>How many definitions were created, updated and deleted.</returns>
    Task<(int Created, int Updated, int Deleted)> UpsertAsync(int expansionId, IReadOnlyList<RaidBuffDefinition> incoming, bool pruneMissing, CancellationToken cancellationToken = default);

    /// <summary>Returns the definition identified by <paramref name="id"/> (without its sources), or <c>null</c> if no such row exists.</summary>
    Task<RaidBuffDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns the definition of <paramref name="spellId"/> on <paramref name="expansionId"/> (without its sources), or <c>null</c> if that spell has none.</summary>
    Task<RaidBuffDefinition?> GetBySpellAsync(int expansionId, int spellId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrites the definition identified by <paramref name="id"/> with <paramref name="incoming"/> — every field,
    /// its spell included, and its sources replaced. The definition keeps its expansion. Returns <c>false</c> if no such row exists.
    /// </summary>
    Task<bool> UpdateAsync(int id, RaidBuffDefinition incoming, CancellationToken cancellationToken = default);

    /// <summary>Deletes the definition identified by <paramref name="id"/>, cascading its sources. Returns <c>false</c> if no such row exists.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
