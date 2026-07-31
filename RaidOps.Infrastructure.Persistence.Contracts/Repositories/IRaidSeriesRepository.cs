using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for persisting and reading <see cref="RaidSeries"/> templates.</summary>
public interface IRaidSeriesRepository
{
    /// <summary>Returns the series identified by <paramref name="id"/> on <paramref name="guildBranchId"/>, including its default zones, or <c>null</c> if not found.</summary>
    Task<RaidSeries?> GetByIdAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Returns every series belonging to <paramref name="guildBranchId"/>, including their default zones.</summary>
    Task<List<RaidSeries>> GetByGuildBranchIdAsync(int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new series along with its default zones.</summary>
    Task<RaidSeries> AddAsync(RaidSeries series, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the scalar fields of the series identified by <paramref name="series"/>.<see cref="RaidSeries.Id"/>
    /// and replaces its default-zone set atomically. Returns <c>false</c> if no matching series exists
    /// on <paramref name="guildBranchId"/>.
    /// </summary>
    Task<bool> UpdateAsync(RaidSeries series, int guildBranchId, IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default);

    /// <summary>Sets <see cref="RaidSeries.IsActive"/> to <c>false</c>. Returns <c>false</c> if no matching series exists on <paramref name="guildBranchId"/>.</summary>
    Task<bool> DeactivateAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);
}
