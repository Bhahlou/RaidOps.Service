using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>
/// A single row of the guild's Discord notification settings, returned by
/// <see cref="Queries.GetGuildNotificationSettingsQuery"/>.
/// </summary>
public class GuildNotificationSettingResponse
{
    /// <summary>The event type this row configures.</summary>
    public GuildNotificationEventType EventType { get; set; }

    /// <summary>Whether the bot posts to <see cref="ChannelId"/> when this event occurs.</summary>
    public bool Enabled { get; set; }

    /// <summary>Discord snowflake ID of the configured channel, or <c>null</c> if none is set yet.</summary>
    public string? ChannelId { get; set; }
}
