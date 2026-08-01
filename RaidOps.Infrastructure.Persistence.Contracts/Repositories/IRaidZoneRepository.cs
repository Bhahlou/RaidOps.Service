using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Read-only access to the static <see cref="RaidZone"/> reference table and its
/// <see cref="RaidLockoutCadenceOverride"/> corrections.
/// </summary>
public interface IRaidZoneRepository
{
    /// <summary>Returns all raid zones ordered by <see cref="RaidZone.SortOrder"/>.</summary>
    Task<List<RaidZone>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns every raid zone belonging to the given expansion, ordered by <see cref="RaidZone.SortOrder"/>.</summary>
    Task<List<RaidZone>> GetByExpansionIdAsync(int expansionId, CancellationToken cancellationToken = default);

    /// <summary>Returns the raid zone with the given ID, including its lockout overrides, or <c>null</c> if not found.</summary>
    Task<RaidZone?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns the raid zones matching the given IDs, including their lockout overrides.</summary>
    Task<List<RaidZone>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Returns the guild's per-zone lockout baseline corrections (e.g. a region reset day) for the given zones, if any exist.</summary>
    Task<List<GuildRaidZoneLockout>> GetGuildOverridesAsync(string guildId, IEnumerable<int> zoneIds, CancellationToken cancellationToken = default);
}
