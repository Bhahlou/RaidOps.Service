using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Settings.Commands;

/// <summary>
/// Command that removes a branch's notification-settings override for one event type, reverting
/// just that setting to inheriting the guild-wide fallback. The requesting user must be an officer
/// of the target branch.
/// </summary>
public class ResetGuildNotificationSettingsCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user requesting the reset. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The branch whose override to remove. Set by the controller, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>The event type whose override to remove. Set by the controller, not from the request body.</summary>
    public GuildNotificationEventType EventType { get; set; }
}
