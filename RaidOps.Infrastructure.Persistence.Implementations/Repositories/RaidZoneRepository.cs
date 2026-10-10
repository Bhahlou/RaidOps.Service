using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRaidZoneRepository"/>.
/// Reads the seeded <see cref="RaidZone"/> reference table and its lockout overrides — no writes at runtime.
/// </summary>
public class RaidZoneRepository(RaidOpsDbContext context) : IRaidZoneRepository
{
    /// <inheritdoc/>
    public async Task<List<RaidZone>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.RaidZones
            .AsNoTracking()
            .OrderBy(z => z.SortOrder)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidZone>> GetByExpansionIdAsync(int expansionId, CancellationToken cancellationToken = default)
        => await context.RaidZones
            .Where(z => z.ExpansionId == expansionId)
            .AsNoTracking()
            .OrderBy(z => z.SortOrder)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidZone?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.RaidZones
            .Include(z => z.LockoutOverrides)
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidZone>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await context.RaidZones
            .Where(z => idList.Contains(z.Id))
            .Include(z => z.LockoutOverrides)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<GuildRaidZoneLockout>> GetGuildOverridesAsync(string guildId, IEnumerable<int> zoneIds, CancellationToken cancellationToken = default)
    {
        var idList = zoneIds.ToList();
        return await context.GuildRaidZoneLockouts
            .Where(l => l.GuildId == guildId && idList.Contains(l.RaidZoneId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
