using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// A guild's Discord notification preference for a single <see cref="GuildNotificationEventType"/>,
/// optionally overridden per branch. Absence of a row for a given (guild, event type, branch) triple
/// means the event is disabled for that scope — rows are only written once an admin explicitly turns
/// an event on, so new event types never need a backfill migration. When <see cref="GuildBranchId"/>
/// is <c>null</c> the row is the guild-wide fallback, used for any branch without its own override row.
/// Surrogate <see cref="Id"/> primary key — Postgres primary keys cannot contain a nullable column,
/// so uniqueness of (GuildId, EventType, GuildBranchId) is enforced instead by two partial unique
/// indexes (one per branch, one for the single guild-wide fallback row).
/// </summary>
[Table("GuildNotificationSettings")]
public class GuildNotificationSetting
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the guild this setting belongs to.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The domain event this setting configures notifications for.</summary>
    public GuildNotificationEventType EventType { get; set; }

    /// <summary>FK to the specific branch this setting overrides, or <c>null</c> for the guild-wide fallback row.</summary>
    public int? GuildBranchId { get; set; }

    /// <summary>Whether the bot should post to <see cref="ChannelId"/> when this event occurs.</summary>
    public bool Enabled { get; set; }

    /// <summary>Discord snowflake ID of the channel to post to, or <c>null</c> if none is configured yet.</summary>
    public string? ChannelId { get; set; }
}
