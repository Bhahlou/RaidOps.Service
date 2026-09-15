using RaidOps.Domain.Models.Raids.Attributions;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="GuildAttributionDefinition"/> persistence.</summary>
public interface IGuildAttributionDefinitionsRepository
{
    /// <summary>Returns every definition of the guild, ordered by <see cref="GuildAttributionDefinition.SortOrder"/>.</summary>
    Task<List<GuildAttributionDefinition>> GetForGuildAsync(string guildId, CancellationToken cancellationToken = default);

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
}
