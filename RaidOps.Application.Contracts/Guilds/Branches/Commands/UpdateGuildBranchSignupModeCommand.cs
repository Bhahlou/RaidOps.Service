using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Branches.Commands;

/// <summary>
/// Command that persists the default signup mode for new raid events created on one guild branch.
/// The requesting user must be an admin of the guild, or hold Officer access on this specific branch.
/// </summary>
public class UpdateGuildBranchSignupModeCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild the branch belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user applying the setting. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch to configure. Set by the controller, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>How attendance is determined by default for new raid events created on this branch.</summary>
    public required SignupMode SignupMode { get; set; }
}
