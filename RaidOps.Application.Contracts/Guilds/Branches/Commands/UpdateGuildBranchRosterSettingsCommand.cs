using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Branches.Commands;

/// <summary>
/// Command that persists the roster/officer role-set configuration for one guild branch. The
/// requesting user must be an admin of the guild, or hold Officer access on this specific branch.
/// </summary>
public class UpdateGuildBranchRosterSettingsCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild the branch belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user applying the settings. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch to configure. Set by the controller, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Controls who may join this branch's roster.</summary>
    public required RosterMode RosterMode { get; set; }

    /// <summary>
    /// Discord snowflake IDs of the roles that grant roster access on this branch. Holding any one
    /// is sufficient. Only relevant when <see cref="RosterMode"/> is <see cref="RosterMode.DiscordRoleOnly"/>.
    /// </summary>
    public List<string> RosterRoleIds { get; set; } = [];

    /// <summary>
    /// Discord snowflake IDs of the roles that grant Officer access on this branch. Holding any one
    /// is sufficient, independently of <see cref="RosterMode"/>.
    /// </summary>
    public List<string> OfficerRoleIds { get; set; } = [];
}
