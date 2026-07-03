using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Notifications.Services;

/// <summary>
/// Default <see cref="IUserNotificationService"/> implementation. Fans out to every registered
/// <see cref="INotificationSignalProvider"/>, then filters the combined result against the
/// user's dismissal ledger.
/// </summary>
public class UserNotificationService(
    IEnumerable<INotificationSignalProvider> providers,
    INotificationDismissalRepository notificationDismissalRepository) : IUserNotificationService
{
    /// <inheritdoc/>
    public async Task<List<NotificationResponse>> GetActiveNotificationsAsync(
        string discordId, IReadOnlyList<UserGuild> eligibleGuilds, CancellationToken cancellationToken = default)
    {
        var dismissed = await notificationDismissalRepository.GetDismissedKeysAsync(discordId, cancellationToken);

        var notifications = new List<NotificationResponse>();
        foreach (var provider in providers)
        {
            var candidates = await provider.GetActiveAsync(discordId, eligibleGuilds, cancellationToken);
            notifications.AddRange(candidates.Where(n => !dismissed.Contains((n.Type, n.GuildId))));
        }

        return notifications;
    }
}
