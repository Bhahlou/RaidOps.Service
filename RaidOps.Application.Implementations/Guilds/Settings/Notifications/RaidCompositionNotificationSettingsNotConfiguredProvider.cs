using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.Notifications;

/// <summary>
/// Surfaces <see cref="NotificationType.RaidCompositionNotificationsNotConfigured"/> for admins of
/// an already-configured guild that has never saved the "Raid composition changes" Discord
/// notification family — distinguished from a guild that deliberately saved the tab with every
/// event left off (a legitimate steady state) by the mere *presence* of a row, same shape as
/// <see cref="AbsenceNotificationSettingsNotConfiguredProvider"/>.
/// </summary>
public class RaidCompositionNotificationSettingsNotConfiguredProvider(
    IGuildNotificationSettingsRepository notificationSettingsRepository) : INotificationSignalProvider
{
    private static readonly GuildNotificationEventType[] RaidCompositionFamilyEventTypes =
    [
        GuildNotificationEventType.RaidSlotAssigned,
        GuildNotificationEventType.RaidSlotUnassigned,
        GuildNotificationEventType.RaidSlotsSwapped,
        GuildNotificationEventType.RaidSlotSpecChanged,
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
            if (settings.Any(s => RaidCompositionFamilyEventTypes.Contains(s.EventType)))
                continue;

            notifications.Add(new NotificationResponse
            {
                Type = NotificationType.RaidCompositionNotificationsNotConfigured,
                GuildId = ug.GuildId,
                GuildName = ug.Guild.Name,
            });
        }

        return notifications;
    }
}
