using RaidOps.Domain.Models.Raids.Attributions;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="GuildAttributionDefinition"/> persistence. Every definition
/// belongs to one guild branch, so each operation is scoped to a (guild, guild branch) pair — the
/// guild ID is kept alongside the branch ID as defense in depth, never as the only scope.
/// </summary>
public interface IGuildAttributionDefinitionsRepository
{
    /// <summary>
    /// Returns every definition of the guild branch scoped to <paramref name="raidBossId"/> (<c>null</c>
    /// for "General" rows), ordered by <see cref="GuildAttributionDefinition.SortOrder"/>. Each row's
    /// spells are loaded with all their per-expansion availabilities so callers can resolve name/icon
    /// for the branch's expansion.
    /// </summary>
    Task<List<GuildAttributionDefinition>> GetForBranchAsync(string guildId, int guildBranchId, int? raidBossId, CancellationToken cancellationToken = default);

    /// <summary>Returns the definition identified by <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<GuildAttributionDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new definition, positioned among its (guild branch, boss) siblings — see the implementation for the section-grouping rule.</summary>
    Task<GuildAttributionDefinition> AddAsync(GuildAttributionDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Updates the scalar fields of the definition identified by <paramref name="definition"/>.<see cref="GuildAttributionDefinition.Id"/>. Returns <c>false</c> if no matching row exists on the guild branch.</summary>
    Task<bool> UpdateAsync(GuildAttributionDefinition definition, string guildId, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the definition identified by <paramref name="id"/> on the guild branch, cascading its per-event fills. Returns <c>false</c> if no matching row exists.</summary>
    Task<bool> DeleteAsync(int id, string guildId, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Re-numbers <see cref="GuildAttributionDefinition.SortOrder"/> for the guild branch's definitions to match <paramref name="orderedIds"/>'s order. IDs not belonging to the guild branch are ignored.</summary>
    Task ReorderAsync(string guildId, int guildBranchId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the section-header icon fields on every row of the guild branch scoped to
    /// <paramref name="raidBossId"/> whose trimmed <see cref="GuildAttributionDefinition.Section"/>
    /// matches <paramref name="section"/> (trimmed). Returns the number of rows updated — <c>0</c>
    /// means no row currently uses that section.
    /// </summary>
    Task<int> SetSectionIconAsync(
        string guildId,
        int guildBranchId,
        int? raidBossId,
        string section,
        SectionIconFields icon,
        CancellationToken cancellationToken = default);
}
