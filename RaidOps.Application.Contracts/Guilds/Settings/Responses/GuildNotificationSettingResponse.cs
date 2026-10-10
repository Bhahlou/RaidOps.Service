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

    /// <summary>
    /// The branch this row is an explicit override for, or <c>null</c> when it's the guild-wide row
    /// (either because that's what was requested, or because the requested branch has no override
    /// and this is the inherited fallback).
    /// </summary>
    public int? GuildBranchId { get; set; }
}
