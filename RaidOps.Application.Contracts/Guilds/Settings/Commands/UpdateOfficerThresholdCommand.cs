using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Settings.Commands;

/// <summary>
/// Command that sets the guild's Officer role threshold — the minimum Discord role position that
/// grants Officer access. Independent of <see cref="RaidOps.Domain.Enums.RosterMode"/>.
/// The requesting user must be an admin of the target guild.
/// </summary>
public class UpdateOfficerThresholdCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild to configure. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user applying the threshold. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>
    /// Discord snowflake ID of the minimum role that grants Officer access. Every guild is
    /// expected to designate one — the Discord Administrator/owner safety net applies on top
    /// regardless, so this can never lock an admin out.
    /// </summary>
    public required string MinOfficerRoleId { get; set; }
}
