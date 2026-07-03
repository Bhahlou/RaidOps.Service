using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// Records that a user dismissed a derived notification, so it stops being surfaced even though
/// the underlying condition that produced it is still true. Not an event log — no notification
/// "creation" is ever persisted, only this dismissal.
/// Composite primary key: (<see cref="UserDiscordId"/>, <see cref="Type"/>, <see cref="GuildId"/>).
/// </summary>
[Table("NotificationDismissals")]
public class NotificationDismissal
{
    /// <summary>Discord snowflake ID of the user who dismissed the notification.</summary>
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>The kind of notification that was dismissed.</summary>
    public NotificationType Type { get; set; }

    /// <summary>Discord snowflake ID of the guild the notification was scoped to.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when the notification was dismissed.</summary>
    public DateTime DismissedAt { get; set; }
}
