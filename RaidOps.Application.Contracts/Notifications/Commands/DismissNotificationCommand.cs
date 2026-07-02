using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Notifications.Commands;

/// <summary>
/// Command that records a user's dismissal of a derived in-app notification, so it stops being
/// surfaced even if the underlying condition that produced it is still true.
/// </summary>
public class DismissNotificationCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the user dismissing the notification. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The kind of notification being dismissed.</summary>
    public required NotificationType Type { get; set; }

    /// <summary>Discord snowflake ID of the guild the notification is scoped to.</summary>
    public required string GuildId { get; set; }
}
