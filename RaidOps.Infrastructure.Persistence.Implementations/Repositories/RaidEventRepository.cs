using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRaidEventRepository"/>. Implemented directly against
/// the context, without <see cref="BaseRepository{TEntity}"/>, since updating an event requires
/// atomically replacing its target-zone set rather than CRUD'ing rows individually.
/// </summary>
public class RaidEventRepository(RaidOpsDbContext context) : IRaidEventRepository
{
    /// <inheritdoc/>
    public async Task<RaidEvent?> GetByIdAsync(int id, int guildBranchId, CancellationToken cancellationToken = default)
        => await context.RaidEvents
            .Where(e => e.Id == id && e.GuildBranchId == guildBranchId)
            .Include(e => e.TargetZones).ThenInclude(z => z.RaidZone)
            .Include(e => e.Assignments).ThenInclude(a => a.Character).ThenInclude(c => c.Class)
            .Include(e => e.Assignments).ThenInclude(a => a.Spec)
            .Include(e => e.GuildBranch).ThenInclude(gb => gb.Branch)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidEvent>> GetForGuildBranchInRangeAsync(int guildBranchId, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default)
        => await context.RaidEvents
            .Where(e => e.GuildBranchId == guildBranchId && e.StartsAtUtc >= rangeStartUtc && e.StartsAtUtc <= rangeEndUtc)
            .Include(e => e.TargetZones).ThenInclude(z => z.RaidZone)
            .Include(e => e.Assignments).ThenInclude(a => a.Character).ThenInclude(c => c.Class)
            .Include(e => e.Assignments).ThenInclude(a => a.Spec)
            .Include(e => e.GuildBranch).ThenInclude(gb => gb.Branch)
            .AsSplitQuery()
            .AsNoTracking()
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> ExistsForSeriesAndDateAsync(int raidSeriesId, DateTime startsAtUtc, CancellationToken cancellationToken = default)
        => await context.RaidEvents
            .AnyAsync(e => e.RaidSeriesId == raidSeriesId && e.StartsAtUtc == startsAtUtc, cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidEvent> AddAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        context.RaidEvents.Add(raidEvent);
        await context.SaveChangesAsync(cancellationToken);
        return raidEvent;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(RaidEvent raidEvent, int guildBranchId, IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default)
    {
        var existing = await context.RaidEvents
            .FirstOrDefaultAsync(e => e.Id == raidEvent.Id && e.GuildBranchId == guildBranchId, cancellationToken);
        if (existing == null) return false;

        existing.Name = raidEvent.Name;
        existing.StartsAtUtc = raidEvent.StartsAtUtc;
        existing.GroupCount = raidEvent.GroupCount;
        existing.SlotsPerGroup = raidEvent.SlotsPerGroup;
        existing.UpdatedAt = raidEvent.UpdatedAt;
        await context.SaveChangesAsync(cancellationToken);

        await context.RaidEventZones
            .Where(z => z.RaidEventId == raidEvent.Id)
            .ExecuteDeleteAsync(cancellationToken);

        // Clear tracker to avoid relationship-fixup conflicts from accumulated state.
        context.ChangeTracker.Clear();

        var freshZones = raidZoneIds.Select(zoneId => new RaidEventZone
        {
            RaidEventId = raidEvent.Id,
            RaidZoneId = zoneId,
        }).ToList();

        context.RaidEventZones.AddRange(freshZones);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> PublishAsync(int id, int guildBranchId, string publishedByDiscordId, CancellationToken cancellationToken = default)
    {
        var count = await context.RaidEvents
            .Where(e => e.Id == id && e.GuildBranchId == guildBranchId)
            .ExecuteUpdateAsync(e => e
                .SetProperty(x => x.PublicationStatus, RaidPublicationStatus.Published)
                .SetProperty(x => x.PublishedAt, DateTime.UtcNow)
                .SetProperty(x => x.PublishedByDiscordId, publishedByDiscordId), cancellationToken);
        return count > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var raidEvent = await context.RaidEvents
            .FirstOrDefaultAsync(e => e.Id == id && e.GuildBranchId == guildBranchId, cancellationToken);
        if (raidEvent == null) return false;

        context.RaidEvents.Remove(raidEvent);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task UpdateCompositionAnnouncementReferenceAsync(int id, int guildBranchId, string channelId, string messageId, CancellationToken cancellationToken = default)
    {
        await context.RaidEvents
            .Where(e => e.Id == id && e.GuildBranchId == guildBranchId)
            .ExecuteUpdateAsync(e => e
                .SetProperty(x => x.CompositionAnnouncementChannelId, channelId)
                .SetProperty(x => x.CompositionAnnouncementMessageId, messageId), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteEmptyDraftOccurrencesForSeriesAsync(int raidSeriesId, int guildBranchId, CancellationToken cancellationToken = default)
        => await context.RaidEvents
            .Where(e => e.RaidSeriesId == raidSeriesId
                && e.GuildBranchId == guildBranchId
                && e.PublicationStatus == RaidPublicationStatus.Draft
                && !e.Assignments.Any())
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidEvent>> GetUpcomingPublishedForGuildAsync(string guildId, DateTime fromUtc, int limit, CancellationToken cancellationToken = default)
        => await context.RaidEvents
            .Where(e => e.GuildId == guildId && e.PublicationStatus == RaidPublicationStatus.Published && e.StartsAtUtc >= fromUtc)
            .Include(e => e.GuildBranch).ThenInclude(gb => gb.Branch)
            .AsNoTracking()
            .OrderBy(e => e.StartsAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
