using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Read-only access to the static <see cref="RaidBoss"/> reference table.</summary>
public interface IRaidBossRepository
{
    /// <summary>Returns the boss with the given ID, including its zone, or <c>null</c> if not found.</summary>
    Task<RaidBoss?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns every boss belonging to the given zones, including their zone, ordered by zone then boss <see cref="RaidBoss.SortOrder"/>.</summary>
    Task<List<RaidBoss>> GetForZonesAsync(IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default);
}
