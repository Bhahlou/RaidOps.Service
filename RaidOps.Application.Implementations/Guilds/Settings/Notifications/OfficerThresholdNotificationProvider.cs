using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Implementations.Guilds.Settings.Notifications;

/// <summary>
/// Surfaces <see cref="NotificationType.OfficerThresholdNotConfigured"/> for admins of an
/// already-configured guild that has no <see cref="Guild.MinOfficerRoleId"/> set yet. Never fires
/// mid get-started onboarding (guild not yet configured) or for non-admins. No repository lookup
/// needed — <see cref="Guild.MinOfficerRoleId"/> is already loaded on the guilds the caller passes in.
/// </summary>
public class OfficerThresholdNotificationProvider : INotificationSignalProvider
{
    /// <inheritdoc/>
    public Task<List<NotificationResponse>> GetActiveAsync(
        string discordId, IReadOnlyList<UserGuild> eligibleGuilds, CancellationToken cancellationToken = default)
    {
        var notifications = new List<NotificationResponse>();
        foreach (var ug in eligibleGuilds)
        {
            var isConfigured = ug.Guild.Timezone != null && ug.Guild.RosterMode != null;
            if (!ug.IsAdmin || !ug.Guild.IsRegistered || !isConfigured)
                continue;

            if (ug.Guild.MinOfficerRoleId != null)
                continue;

            notifications.Add(new NotificationResponse
            {
                Type = NotificationType.OfficerThresholdNotConfigured,
                GuildId = ug.GuildId,
                GuildName = ug.Guild.Name,
            });
        }

        return Task.FromResult(notifications);
    }
}
