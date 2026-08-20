using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Maintains the standing "current composition" Discord announcement for a published raid event,
/// and DMs players when they're added to or removed from it — the two independent halves of the
/// "raid composition announcement" guild notification family (<see cref="RaidOps.Domain.Enums.GuildNotificationEventType.RaidCompositionAnnouncementPosted"/>/
/// <see cref="RaidOps.Domain.Enums.GuildNotificationEventType.RaidCompositionAnnouncementDm"/>).
/// Every method here resolves its own setting and no-ops silently if disabled/unconfigured or if
/// the Discord call fails — same contract as <see cref="IGuildNotificationDispatcher"/>, this must
/// never fail the caller's own command.
/// </summary>
public interface IRaidCompositionAnnouncementService
{
    /// <summary>
    /// Posts the standing composition embed if it's never been posted for this event yet, or edits
    /// it in place to reflect the current roster otherwise. Callers only need to call this after
    /// any change to <paramref name="raidEvent"/>'s composition (or on first publish) — it always
    /// re-reads the current assignments itself, since <paramref name="raidEvent"/> may be a
    /// pre-change snapshot.
    /// </summary>
    Task PublishOrUpdateAnnouncementAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// DMs a player who was just assigned a slot in this raid event, if enabled — including when
    /// the raid is published with them already assigned from the draft phase, since the public
    /// embed intentionally never pings anyone (it's edited in place on every change, so pinging
    /// would spam on every roster edit) and the DM is otherwise the only way they find out.
    /// </summary>
    /// <param name="isInitialPublish">True when this call is for an already-assigned player at the moment the raid is published, rather than a fresh slot assignment on an already-published raid.</param>
    Task NotifyPlayerAddedAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, bool isInitialPublish, CancellationToken cancellationToken = default);

    /// <summary>DMs a player who was just unassigned from this raid event, if enabled.</summary>
    Task NotifyPlayerRemovedAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>DMs a player whose slot assignment's spec was just changed on this raid event, if enabled.</summary>
    Task NotifyPlayerSpecChangedAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the standing composition announcement message, if one was ever posted for this
    /// event — called when the event itself is deleted, so a stale "current composition" message
    /// doesn't linger for a raid that no longer exists. No-ops if none was ever posted.
    /// </summary>
    Task DeleteAnnouncementAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// DMs a player that a published raid they were assigned to has been cancelled. Sent
    /// unconditionally — unlike every other DM on this interface, this one ignores the
    /// <see cref="RaidOps.Domain.Enums.GuildNotificationEventType.RaidCompositionAnnouncementDm"/>
    /// setting entirely, since it's the only guaranteed way a player learns their raid was
    /// cancelled (the public embed never pings, and this DM would otherwise be opt-in like the
    /// rest of the family). Still no-ops silently on a Discord send failure — never fails the
    /// caller's own delete command.
    /// </summary>
    Task NotifyPlayerRaidCancelledAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, CancellationToken cancellationToken = default);
}
