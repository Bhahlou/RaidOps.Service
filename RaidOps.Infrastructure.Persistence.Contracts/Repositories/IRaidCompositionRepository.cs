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

    /// <summary>
    /// Exchanges the characters assigned at two coordinates within the same event — the
    /// counterpart to <see cref="AssignCharacterAsync"/> for a drop onto an already-occupied slot.
    /// Returns <c>false</c> without making any change if either coordinate was empty.
    /// </summary>
    Task<bool> SwapAssignmentsAsync(int raidEventId, int groupNumberA, int slotNumberA, int groupNumberB, int slotNumberB, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the spec an already-assigned character is playing at the given coordinate (e.g.
    /// switching to an off-spec this raid needs). Returns <c>false</c> if the slot was empty.
    /// </summary>
    Task<bool> UpdateAssignmentSpecAsync(int raidEventId, int groupNumber, int slotNumber, int specId, CancellationToken cancellationToken = default);

    /// <summary>Returns every assignment for the given event, including the assigned character (with its class) and spec.</summary>
    Task<List<RaidSlotAssignment>> GetAssignmentsForEventAsync(int raidEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every assignment for <paramref name="characterId"/> across the guild branch's
    /// non-cancelled events, including each event's target zones — used to detect lockout conflicts
    /// before assigning the character to a new event.
    /// </summary>
    Task<List<RaidSlotAssignment>> GetActiveAssignmentsForCharacterInGuildBranchAsync(int characterId, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of character IDs assigned to any of the guild branch's non-cancelled, <b>published</b>
    /// events starting within the given UTC range. Draft-only assignments don't count — a character
    /// assigned solely within a draft still shows up as "unassigned" against the official schedule.
    /// </summary>
    Task<HashSet<int>> GetAssignedCharacterIdsInRangeAsync(int guildBranchId, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default);
}
