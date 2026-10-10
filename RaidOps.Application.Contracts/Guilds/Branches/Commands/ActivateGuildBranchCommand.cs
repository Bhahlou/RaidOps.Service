using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Branches.Commands;

/// <summary>
/// Command that activates a WoW game-version branch on a guild — creating a new
/// <see cref="Domain.Models.Discord.GuildBranch"/> row, or reactivating a previously deactivated
/// one (preserving its prior roster/officer role-set configuration). The requesting user must be
/// an admin of the target guild.
/// </summary>
public class ActivateGuildBranchCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild to activate the branch on. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user activating the branch. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>FK to the WoW game-version branch (Retail, Classic Era, …) to activate.</summary>
    public required int BranchId { get; set; }
}
