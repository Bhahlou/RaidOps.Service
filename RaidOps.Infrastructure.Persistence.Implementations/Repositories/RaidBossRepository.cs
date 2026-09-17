using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRaidBossRepository"/>. Reads the seeded <see cref="RaidBoss"/>
/// reference table — no writes at runtime.
/// </summary>
public class RaidBossRepository(RaidOpsDbContext context) : IRaidBossRepository
{
    /// <inheritdoc/>
    public async Task<RaidBoss?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.RaidBosses
            .Include(b => b.RaidZone)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidBoss>> GetForZonesAsync(IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default)
    {
        var idList = raidZoneIds.ToList();
        return await context.RaidBosses
            .Where(b => idList.Contains(b.RaidZoneId))
            .Include(b => b.RaidZone)
            .AsNoTracking()
            .OrderBy(b => b.RaidZone.SortOrder).ThenBy(b => b.SortOrder)
            .ToListAsync(cancellationToken);
    }
}
