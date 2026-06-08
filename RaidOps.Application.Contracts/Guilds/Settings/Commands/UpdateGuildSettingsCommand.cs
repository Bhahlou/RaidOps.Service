using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Settings.Commands;

/// <summary>
/// Command that persists the settings for a registered guild (timezone, roster mode, role threshold).
/// The requesting user must be an admin of the target guild.
/// </summary>
public class UpdateGuildSettingsCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild to configure. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user applying the settings. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>IANA timezone identifier (e.g. <c>"Europe/Paris"</c>).</summary>
    public required string Timezone { get; set; }

    /// <summary>Controls who may join the guild's roster.</summary>
    public required RosterMode RosterMode { get; set; }

    /// <summary>
    /// Discord snowflake ID of the minimum role required to join the roster.
    /// Only relevant when <see cref="RosterMode"/> is <see cref="RosterMode.DiscordRoleOnly"/>.
    /// </summary>
    public string? MinRosterRoleId { get; set; }
}
