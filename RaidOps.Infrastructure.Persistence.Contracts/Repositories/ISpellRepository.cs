using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="Spell"/> reference-table reads and one-time seeding.</summary>
public interface ISpellRepository
{
    /// <summary>Returns the spell identified by <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<Spell?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> spells of <paramref name="expansionId"/> whose
    /// localized name (matching <paramref name="locale"/>) contains <paramref name="searchTerm"/>,
    /// case-insensitively.
    /// </summary>
    Task<List<Spell>> SearchAsync(int expansionId, string searchTerm, string locale, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts every spell in <paramref name="spells"/> whose <see cref="Spell.Id"/> doesn't already
    /// exist — used by the one-time-per-expansion import seeder. Existing rows are left untouched
    /// (re-running an import is a no-op for spells already seeded).
    /// </summary>
    Task<int> InsertMissingAsync(IEnumerable<Spell> spells, CancellationToken cancellationToken = default);
}
