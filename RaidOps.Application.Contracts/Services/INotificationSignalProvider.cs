using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Computes the notifications a single domain wants to surface to a user, for the guilds they're
/// eligible to see. Each domain implements one provider per notification type it owns — providers
/// are auto-registered like CQRS handlers (Scrutor scan), so <see cref="IUserNotificationService"/>
/// and its callers never need to know which domains exist. Dismissal filtering is NOT this
/// provider's concern — <see cref="IUserNotificationService"/> applies it centrally afterwards,
/// since "was this dismissed" is generic infrastructure, not domain logic.
/// </summary>
public interface INotificationSignalProvider
{
    /// <summary>
    /// Returns the notifications this provider's domain currently considers active for the given
    /// user, before dismissal filtering.
    /// </summary>
    /// <param name="discordId">Discord snowflake ID of the requesting user.</param>
    /// <param name="eligibleGuilds">The guilds the user is either an admin of or a registered member of.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<NotificationResponse>> GetActiveAsync(
        string discordId, IReadOnlyList<UserGuild> eligibleGuilds, CancellationToken cancellationToken = default);
}
