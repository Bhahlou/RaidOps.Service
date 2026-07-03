using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Notifications.Responses;

/// <summary>
/// A derived in-app notification surfaced to a user, e.g. "this guild has no role mapping yet".
/// Never persisted as an event — computed live on each <c>/me</c> call and filtered against the
/// user's dismissal ledger.
/// </summary>
public class NotificationResponse
{
    /// <summary>The kind of notification.</summary>
    public required NotificationType Type { get; set; }

    /// <summary>Discord snowflake ID of the guild this notification is scoped to.</summary>
    public required string GuildId { get; set; }

    /// <summary>Display name of the guild, so the front end can render the message without an extra lookup.</summary>
    public required string GuildName { get; set; }
}
