using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRaidSeriesRepository"/>. Implemented directly against
/// the context, without <see cref="BaseRepository{TEntity}"/>, since updating a series requires
/// atomically replacing its default-zone set rather than CRUD'ing rows individually.
/// </summary>
public class RaidSeriesRepository(RaidOpsDbContext context) : IRaidSeriesRepository
{
    /// <inheritdoc/>
    public async Task<RaidSeries?> GetByIdAsync(int id, string guildId, CancellationToken cancellationToken = default)
        => await context.RaidSeries
            .Where(s => s.Id == id && s.GuildId == guildId)
            .Include(s => s.DefaultZones).ThenInclude(z => z.RaidZone)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidSeries>> GetByGuildIdAsync(string guildId, CancellationToken cancellationToken = default)
        => await context.RaidSeries
            .Where(s => s.GuildId == guildId)
            .Include(s => s.DefaultZones).ThenInclude(z => z.RaidZone)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidSeries> AddAsync(RaidSeries series, CancellationToken cancellationToken = default)
    {
        context.RaidSeries.Add(series);
        await context.SaveChangesAsync(cancellationToken);
        return series;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(RaidSeries series, string guildId, IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default)
    {
        var existing = await context.RaidSeries
            .FirstOrDefaultAsync(s => s.Id == series.Id && s.GuildId == guildId, cancellationToken);
        if (existing == null) return false;

        existing.Name = series.Name;
        existing.BranchId = series.BranchId;
        existing.RecurrenceDayOfWeek = series.RecurrenceDayOfWeek;
        existing.RecurrenceStartTimeLocal = series.RecurrenceStartTimeLocal;
        existing.RecurrenceIntervalWeeks = series.RecurrenceIntervalWeeks;
        existing.GroupCount = series.GroupCount;
        existing.SlotsPerGroup = series.SlotsPerGroup;
        await context.SaveChangesAsync(cancellationToken);

        await context.RaidSeriesZones
            .Where(z => z.RaidSeriesId == series.Id)
            .ExecuteDeleteAsync(cancellationToken);

        // Clear tracker to avoid relationship-fixup conflicts from accumulated state.
        context.ChangeTracker.Clear();

        var freshZones = raidZoneIds.Select(zoneId => new RaidSeriesZone
        {
            RaidSeriesId = series.Id,
            RaidZoneId = zoneId,
        }).ToList();

        context.RaidSeriesZones.AddRange(freshZones);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeactivateAsync(int id, string guildId, CancellationToken cancellationToken = default)
    {
        var count = await context.RaidSeries
            .Where(s => s.Id == id && s.GuildId == guildId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);
        return count > 0;
    }
}
