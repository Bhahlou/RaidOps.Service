using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Settings.Commands;

/// <summary>
/// Command that persists the guild's Discord notification settings in bulk — one row per event
/// type the admin toggled or reconfigured. The requesting user must be an admin of the target guild.
/// </summary>
public class UpdateGuildNotificationSettingsCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild to configure. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user applying the settings. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The settings to persist, one row per event type.</summary>
    public required List<GuildNotificationSettingInput> Settings { get; set; }
}
