using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.Notifications;

/// <summary>
/// Surfaces <see cref="NotificationType.AbsenceNotificationsNotConfigured"/> for admins of an
/// already-configured guild that has never saved the "Absences" Discord notification family —
/// distinguished from a guild that deliberately saved the tab with every event left off (a
/// legitimate steady state) by the mere *presence* of a row: the front always upserts a row per
/// known event type on every save, enabled or not, so "no row for either event" can only mean the
/// admin has never saved this family's tab since it existed. A future notification family (e.g.
/// raid reminders) gets the exact same shape: its own <see cref="NotificationType"/> plus its own
/// provider checking its own event set, without touching this one.
/// </summary>
public class AbsenceNotificationSettingsNotConfiguredProvider(
    IGuildNotificationSettingsRepository notificationSettingsRepository) : INotificationSignalProvider
{
    private static readonly GuildNotificationEventType[] AbsenceFamilyEventTypes =
    [
        GuildNotificationEventType.AbsenceAdded,
        GuildNotificationEventType.AbsenceRemoved,
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
            if (settings.Any(s => AbsenceFamilyEventTypes.Contains(s.EventType)))
                continue;

            notifications.Add(new NotificationResponse
            {
                Type = NotificationType.AbsenceNotificationsNotConfigured,
                GuildId = ug.GuildId,
                GuildName = ug.Guild.Name,
            });
        }

        return notifications;
    }
}
