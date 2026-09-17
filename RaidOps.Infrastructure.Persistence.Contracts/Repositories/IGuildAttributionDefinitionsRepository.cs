using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="GuildAttributionDefinition"/> persistence.</summary>
public interface IGuildAttributionDefinitionsRepository
{
    /// <summary>
    /// Returns every definition of the guild scoped to <paramref name="raidBossId"/> (<c>null</c> for
    /// "General" rows), ordered by <see cref="GuildAttributionDefinition.SortOrder"/>.
    /// </summary>
    Task<List<GuildAttributionDefinition>> GetForGuildAsync(string guildId, int? raidBossId, CancellationToken cancellationToken = default);

    /// <summary>Returns the definition identified by <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<GuildAttributionDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new definition, appended after the guild's current highest <see cref="GuildAttributionDefinition.SortOrder"/>.</summary>
    Task<GuildAttributionDefinition> AddAsync(GuildAttributionDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Updates the scalar fields of the definition identified by <paramref name="definition"/>.<see cref="GuildAttributionDefinition.Id"/>. Returns <c>false</c> if no matching row exists on <paramref name="guildId"/>.</summary>
    Task<bool> UpdateAsync(GuildAttributionDefinition definition, string guildId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the definition identified by <paramref name="id"/> on <paramref name="guildId"/>, cascading its per-event fills. Returns <c>false</c> if no matching row exists.</summary>
    Task<bool> DeleteAsync(int id, string guildId, CancellationToken cancellationToken = default);

    /// <summary>Re-numbers <see cref="GuildAttributionDefinition.SortOrder"/> for the guild's definitions to match <paramref name="orderedIds"/>'s order. IDs not belonging to the guild are ignored.</summary>
    Task ReorderAsync(string guildId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the section-header icon fields on every row of <paramref name="guildId"/> scoped to
    /// <paramref name="raidBossId"/> whose trimmed <see cref="GuildAttributionDefinition.Section"/>
    /// matches <paramref name="section"/> (trimmed). Returns the number of rows updated — <c>0</c>
    /// means no row currently uses that section.
    /// </summary>
    Task<int> SetSectionIconAsync(
        string guildId,
        int? raidBossId,
        string section,
        AttributionIconSource iconSource,
        int? spellId,
        RaidMarkerIcon? raidMarker,
        SpecRole? staticRole,
        CancellationToken cancellationToken = default);
}
