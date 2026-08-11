namespace RaidOps.Domain.Enums;

/// <summary>
/// Identifies a domain event a guild can opt into being notified about on Discord, via
/// <see cref="Models.Discord.GuildNotificationSetting"/>. Unrelated to <see cref="NotificationType"/>,
/// which drives the in-app notification bell — this enum drives messages the bot posts to a
/// guild-configured Discord channel.
/// </summary>
public enum GuildNotificationEventType
{
    /// <summary>
    /// A member adds an absence: a one-off exception declared with
    /// <see cref="DayAvailabilityStatus.Absent"/> or <see cref="DayAvailabilityStatus.Partial"/>,
    /// or a new recurring availability pattern.
    /// </summary>
    AbsenceAdded = 1,

    /// <summary>
    /// A member removes an absence: a one-off exception deleted, a recurring pattern stopped, or
    /// a one-off exception declared with <see cref="DayAvailabilityStatus.Available"/> — which by
    /// definition only makes sense to cancel what a recurring pattern would otherwise have marked
    /// as unavailable on that day.
    /// </summary>
    AbsenceRemoved = 2,

    /// <summary>A raid event was published, becoming visible to non-officer roster members.</summary>
    RaidPublished = 3,

    /// <summary>An already-published raid event was deleted (there is no separate "cancel" flow).</summary>
    RaidCancelled = 4,

    /// <summary>An already-published raid event's start time was changed.</summary>
    RaidRescheduled = 5,

    /// <summary>A character was assigned to a slot on an already-published raid event.</summary>
    RaidSlotAssigned = 6,

    /// <summary>A character was unassigned from a slot on an already-published raid event.</summary>
    RaidSlotUnassigned = 7,

    /// <summary>Two characters' slots were swapped on an already-published raid event.</summary>
    RaidSlotsSwapped = 8,

    /// <summary>A slot assignment's spec was changed on an already-published raid event.</summary>
    RaidSlotSpecChanged = 9,

    /// <summary>
    /// The standing "current composition" embed for a published raid event is posted/updated in a
    /// Discord channel — edited in place as the roster changes, rather than a new message per
    /// change like <see cref="RaidSlotAssigned"/> and friends.
    /// </summary>
    RaidCompositionAnnouncementPosted = 10,

    /// <summary>
    /// A player is DMed because they were added to or removed from a published raid event's
    /// composition. Independent of <see cref="RaidCompositionAnnouncementPosted"/> — either, both,
    /// or neither can be enabled.
    /// </summary>
    RaidCompositionAnnouncementDm = 11,
}
