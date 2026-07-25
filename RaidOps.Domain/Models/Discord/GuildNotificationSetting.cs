using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// A guild's Discord notification preference for a single <see cref="GuildNotificationEventType"/>.
/// Absence of a row for a given (guild, event type) pair means the event is disabled — rows are
/// only written once an admin explicitly turns an event on, so new event types never need a
/// backfill migration.
/// </summary>
[Table("GuildNotificationSettings")]
public class GuildNotificationSetting
{
    /// <summary>Discord snowflake ID of the guild this setting belongs to.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The domain event this setting configures notifications for.</summary>
    public GuildNotificationEventType EventType { get; set; }

    /// <summary>Whether the bot should post to <see cref="ChannelId"/> when this event occurs.</summary>
    public bool Enabled { get; set; }

    /// <summary>Discord snowflake ID of the channel to post to, or <c>null</c> if none is configured yet.</summary>
    public string? ChannelId { get; set; }
}
