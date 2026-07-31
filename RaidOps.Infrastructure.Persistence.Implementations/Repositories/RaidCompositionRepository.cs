using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRaidCompositionRepository"/>.
/// Implemented directly against the context, without a base class, since assignment is a
/// replace-not-add operation to support drag-repositioning within an event.
/// </summary>
public class RaidCompositionRepository(RaidOpsDbContext context) : IRaidCompositionRepository
{
    /// <inheritdoc/>
    public async Task AssignCharacterAsync(RaidSlotAssignment assignment, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Remove any prior assignment of this character within the event first, so repositioning
        // it to a new coordinate is a single replacement rather than a duplicate row.
        await context.RaidSlotAssignments
            .Where(a => a.RaidEventId == assignment.RaidEventId && a.CharacterId == assignment.CharacterId)
            .ExecuteDeleteAsync(cancellationToken);

        // Clear tracker to avoid relationship-fixup conflicts from accumulated state.
        context.ChangeTracker.Clear();

        context.RaidSlotAssignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> UnassignAsync(int raidEventId, int groupNumber, int slotNumber, CancellationToken cancellationToken = default)
    {
        var count = await context.RaidSlotAssignments
            .Where(a => a.RaidEventId == raidEventId && a.GroupNumber == groupNumber && a.SlotNumber == slotNumber)
            .ExecuteDeleteAsync(cancellationToken);
        return count > 0;
    }

    /// <inheritdoc/>
    public async Task<List<RaidSlotAssignment>> GetAssignmentsForEventAsync(int raidEventId, CancellationToken cancellationToken = default)
        => await context.RaidSlotAssignments
            .Where(a => a.RaidEventId == raidEventId)
            .Include(a => a.Character).ThenInclude(c => c.Class)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidSlotAssignment>> GetActiveAssignmentsForCharacterInGuildBranchAsync(int characterId, int guildBranchId, CancellationToken cancellationToken = default)
        => await context.RaidSlotAssignments
            .Where(a => a.CharacterId == characterId
                && a.RaidEvent.GuildBranchId == guildBranchId
                && a.RaidEvent.Status != RaidEventStatus.Cancelled)
            .Include(a => a.RaidEvent).ThenInclude(e => e.TargetZones)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<HashSet<int>> GetAssignedCharacterIdsInRangeAsync(int guildBranchId, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default)
    {
        var ids = await context.RaidSlotAssignments
            .Where(a => a.RaidEvent.GuildBranchId == guildBranchId
                && a.RaidEvent.Status != RaidEventStatus.Cancelled
                && a.RaidEvent.PublicationStatus == RaidPublicationStatus.Published
                && a.RaidEvent.StartsAtUtc >= rangeStartUtc
                && a.RaidEvent.StartsAtUtc <= rangeEndUtc)
            .AsNoTracking()
            .Select(a => a.CharacterId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. ids];
    }
}
