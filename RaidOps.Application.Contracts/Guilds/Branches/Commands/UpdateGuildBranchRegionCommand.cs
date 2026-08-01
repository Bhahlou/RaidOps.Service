using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Branches.Commands;

/// <summary>
/// Command that persists the Blizzard API region for one guild branch — used to resolve its weekly
/// raid-lockout schedule. The requesting user must be an admin of the guild, or hold Officer access
/// on this specific branch.
/// </summary>
public class UpdateGuildBranchRegionCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild the branch belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user applying the setting. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch to configure. Set by the controller, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Blizzard API region this branch's realm sits in: "eu", "us", "kr", or "tw".</summary>
    public required string Region { get; set; }
}
