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
}
