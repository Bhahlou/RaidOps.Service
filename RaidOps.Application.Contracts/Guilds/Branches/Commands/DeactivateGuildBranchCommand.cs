using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Branches.Commands;

/// <summary>
/// Command that deactivates a guild branch. Never hard-deletes — roster history and role-set
/// configuration are preserved for a future reactivation. The requesting user must be an admin of
/// the target guild.
/// </summary>
public class DeactivateGuildBranchCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild the branch belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user deactivating the branch. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch to deactivate. Set by the controller, not from the request body.</summary>
    public int GuildBranchId { get; set; }
}
