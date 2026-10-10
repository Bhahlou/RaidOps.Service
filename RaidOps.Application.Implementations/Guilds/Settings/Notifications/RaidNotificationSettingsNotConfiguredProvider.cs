using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.Notifications;

/// <summary>
/// Surfaces <see cref="NotificationType.RaidNotificationsNotConfigured"/> for admins of an
/// already-configured guild that has never saved the "Raid changes" Discord notification family —
/// distinguished from a guild that deliberately saved the tab with every event left off (a
/// legitimate steady state) by the mere *presence* of a row, same shape as
/// <see cref="AbsenceNotificationSettingsNotConfiguredProvider"/>.
/// </summary>
public class RaidNotificationSettingsNotConfiguredProvider(
    IGuildNotificationSettingsRepository notificationSettingsRepository) : INotificationSignalProvider
{
    private static readonly GuildNotificationEventType[] RaidFamilyEventTypes =
    [
        GuildNotificationEventType.RaidPublished,
        GuildNotificationEventType.RaidCancelled,
        GuildNotificationEventType.RaidRescheduled,
    ];

    /// <inheritdoc/>
    public async Task<List<NotificationResponse>> GetActiveAsync(
        string discordId, IReadOnlyList<UserGuild> eligibleGuilds, CancellationToken cancellationToken = default)
    {
        var notifications = new List<NotificationResponse>();
        foreach (var ug in eligibleGuilds)
        {
            if (!ug.IsAdmin || !ug.Guild.IsRegistered || ug.Guild.Timezone == null)
                continue;

            var settings = await notificationSettingsRepository.GetAllForGuildAsync(ug.GuildId, cancellationToken);
            if (settings.Any(s => RaidFamilyEventTypes.Contains(s.EventType)))
                continue;

            notifications.Add(new NotificationResponse
            {
                Type = NotificationType.RaidNotificationsNotConfigured,
                GuildId = ug.GuildId,
                GuildName = ug.Guild.Name,
            });
        }

        return notifications;
    }
}
