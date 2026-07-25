using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for persisting and reading <see cref="RaidSeries"/> templates.</summary>
public interface IRaidSeriesRepository
{
    /// <summary>Returns the series identified by <paramref name="id"/> in <paramref name="guildId"/>, including its default zones, or <c>null</c> if not found.</summary>
    Task<RaidSeries?> GetByIdAsync(int id, string guildId, CancellationToken cancellationToken = default);

    /// <summary>Returns every series belonging to <paramref name="guildId"/>, including their default zones.</summary>
    Task<List<RaidSeries>> GetByGuildIdAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new series along with its default zones.</summary>
    Task<RaidSeries> AddAsync(RaidSeries series, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the scalar fields of the series identified by <paramref name="series"/>.<see cref="RaidSeries.Id"/>
    /// and replaces its default-zone set atomically. Returns <c>false</c> if no matching series exists
    /// in <paramref name="guildId"/>.
    /// </summary>
    Task<bool> UpdateAsync(RaidSeries series, string guildId, IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default);

    /// <summary>Sets <see cref="RaidSeries.IsActive"/> to <c>false</c>. Returns <c>false</c> if no matching series exists in <paramref name="guildId"/>.</summary>
    Task<bool> DeactivateAsync(int id, string guildId, CancellationToken cancellationToken = default);
}
