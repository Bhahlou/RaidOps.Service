using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Aggregates active notifications across every registered <see cref="INotificationSignalProvider"/>
/// and filters out anything the user already dismissed. The single entry point callers (like
/// <c>GetMeQueryHandler</c>) use — they never talk to individual providers directly, so adding a
/// new notification type never requires touching this service or its callers.
/// </summary>
public interface IUserNotificationService
{
    /// <summary>
    /// Returns the notifications currently active for the user across all domains, excluding
    /// anything already dismissed.
    /// </summary>
    /// <param name="discordId">Discord snowflake ID of the requesting user.</param>
    /// <param name="eligibleGuilds">The guilds the user is either an admin of or a registered member of.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<NotificationResponse>> GetActiveNotificationsAsync(
        string discordId, IReadOnlyList<UserGuild> eligibleGuilds, CancellationToken cancellationToken = default);
}
