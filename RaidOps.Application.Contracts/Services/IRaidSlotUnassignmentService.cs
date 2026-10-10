using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Clears a (group, slot) coordinate on a raid event and, if it held an assignment on an
/// already-published event, posts the "raid composition changes" Discord notification — the one
/// piece of behavior shared by every way a slot can become unassigned, whether an officer clears it
/// directly (<c>UnassignSlotCommand</c>) or a player's own RSVP change makes their old slot stale
/// (<c>SetMyRaidSignupCommand</c>).
/// </summary>
public interface IRaidSlotUnassignmentService
{
    /// <summary>
    /// Clears <paramref name="groupNumber"/>/<paramref name="slotNumber"/> on <paramref name="raidEvent"/>.
    /// Returns <c>false</c> if the slot was already empty (no-op, no notification sent).
    /// </summary>
    Task<bool> UnassignAsync(RaidEvent raidEvent, int groupNumber, int slotNumber, string requesterDiscordId, CancellationToken cancellationToken = default);
}
