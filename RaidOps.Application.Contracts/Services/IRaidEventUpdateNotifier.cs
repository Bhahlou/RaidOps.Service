using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Handles the Discord-facing side effects of updating a raid event — moving the dedicated
/// announcement channel's standing embeds when it changed, and posting a "Raid rescheduled"
/// notification when a published event's start time changes. Exists purely to keep the update
/// command handler's constructor from having to inject each of the underlying notification
/// services individually.
/// </summary>
public interface IRaidEventUpdateNotifier
{
    /// <summary>
    /// Drops the standing embeds from <paramref name="existing"/>'s old dedicated channel, clears
    /// their cached references, deletes the old channel itself if RaidOps had created it for this
    /// event, then re-posts the signup-call embed fresh in the new channel for Signup-mode events.
    /// Only call when the dedicated channel actually changed.
    /// </summary>
    Task MoveDedicatedChannelAsync(int eventId, int guildBranchId, RaidEvent existing, CancellationToken cancellationToken = default);

    /// <summary>Builds and dispatches the "Raid rescheduled" notification. Only call when the event is published and its start time actually changed.</summary>
    Task NotifyRescheduledAsync(string guildId, string requesterDiscordId, int guildBranchId, RaidEvent raidEvent, DateTime oldStartsAtUtc, CancellationToken cancellationToken = default);
}
