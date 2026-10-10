using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Settings.Commands;

/// <summary>
/// A single row of <see cref="UpdateGuildNotificationSettingsCommand.Settings"/>.
/// </summary>
public class GuildNotificationSettingInput
{
    /// <summary>The event type this row configures.</summary>
    public required GuildNotificationEventType EventType { get; set; }

    /// <summary>Whether the bot should post to <see cref="ChannelId"/> when this event occurs.</summary>
    public bool Enabled { get; set; }

    /// <summary>Discord snowflake ID of the channel to post to, or <c>null</c> if none is configured.</summary>
    public string? ChannelId { get; set; }
}
