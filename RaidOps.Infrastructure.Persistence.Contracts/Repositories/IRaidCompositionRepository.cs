using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for reading and mutating the sparse <see cref="RaidSlotAssignment"/> grid.
/// Implemented directly against the database context (no generic base) because assigning a
/// character requires an atomic delete-then-insert to support drag-repositioning within an event.
/// </summary>
public interface IRaidCompositionRepository
{
    /// <summary>
    /// Assigns <paramref name="assignment"/>'s character to its (group, slot) coordinate.
    /// Atomically removes any existing assignment for the same character in the same event first,
    /// so a drag from one slot to another within the event is a single replacement, not a duplicate.
    /// </summary>
    Task AssignCharacterAsync(RaidSlotAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>Removes the assignment at the given coordinate. Returns <c>false</c> if the slot was already empty.</summary>
    Task<bool> UnassignAsync(int raidEventId, int groupNumber, int slotNumber, CancellationToken cancellationToken = default);

    /// <summary>Returns every assignment for the given event, including the assigned character.</summary>
    Task<List<RaidSlotAssignment>> GetAssignmentsForEventAsync(int raidEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every assignment for <paramref name="characterId"/> across the guild's non-cancelled
    /// events, including each event's target zones — used to detect lockout conflicts before
    /// assigning the character to a new event.
    /// </summary>
    Task<List<RaidSlotAssignment>> GetActiveAssignmentsForCharacterInGuildAsync(int characterId, string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of character IDs assigned to any of the guild's non-cancelled, <b>published</b>
    /// events starting within the given UTC range. Draft-only assignments don't count — a character
    /// assigned solely within a draft still shows up as "unassigned" against the official schedule.
    /// </summary>
    Task<HashSet<int>> GetAssignedCharacterIdsInRangeAsync(string guildId, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default);
}
