using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for the spell reference data. A spell's name and icon are per expansion, so
/// every read/write here goes through <see cref="SpellAvailability"/> rather than <see cref="Spell"/>.
/// </summary>
public interface ISpellRepository
{
    /// <summary>
    /// Returns what <paramref name="spellId"/> is called and looks like on <paramref name="expansionId"/>,
    /// or <c>null</c> if that spell hasn't been observed on that expansion.
    /// </summary>
    Task<SpellAvailability?> GetAvailabilityAsync(int spellId, int expansionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> spells observed on <paramref name="expansionId"/> whose
    /// localized name on that expansion (matching <paramref name="locale"/>) contains
    /// <paramref name="searchTerm"/>, case-insensitively.
    /// </summary>
    Task<List<SpellAvailability>> SearchAsync(int expansionId, string searchTerm, string locale, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a <see cref="Spell"/> row exists for every spell ID in <paramref name="rows"/>, then
    /// inserts each (spell, expansion) row that doesn't exist yet and updates the names/icon of the
    /// ones that do whenever they've changed. Each row is compared only against its own expansion's
    /// previous value, so syncing several branches that share spell IDs never overwrites one branch's
    /// content with another's. Used by the periodic wago.tools sync.
    /// </summary>
    /// <param name="rows">The (spell, expansion) rows to write.</param>
    /// <returns>The (spell, expansion) rows that were newly inserted and the ones whose English name changed, for reporting.</returns>
    Task<SpellSyncDiff> UpsertAsync(IEnumerable<SpellAvailability> rows, CancellationToken cancellationToken = default);
}
