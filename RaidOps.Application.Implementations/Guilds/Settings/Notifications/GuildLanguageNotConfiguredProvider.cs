using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Implementations.Guilds.Settings.Notifications;

/// <summary>
/// Surfaces <see cref="NotificationType.GuildLanguageNotConfigured"/> for admins of an
/// already-configured guild that has no <see cref="Guild.Language"/> set yet. Never fires mid
/// get-started onboarding (guild not yet configured) or for non-admins. No repository lookup
/// needed — <see cref="Guild.Language"/> is already loaded on the guilds the caller passes in.
/// </summary>
public class GuildLanguageNotConfiguredProvider : INotificationSignalProvider
{
    /// <inheritdoc/>
    public Task<List<NotificationResponse>> GetActiveAsync(
        string discordId, IReadOnlyList<UserGuild> eligibleGuilds, CancellationToken cancellationToken = default)
    {
        var notifications = new List<NotificationResponse>();
        foreach (var ug in eligibleGuilds)
        {
            if (!ug.IsAdmin || !ug.Guild.IsRegistered || ug.Guild.Timezone == null)
                continue;

            if (ug.Guild.Language != null)
                continue;

            notifications.Add(new NotificationResponse
            {
                Type = NotificationType.GuildLanguageNotConfigured,
                GuildId = ug.GuildId,
                GuildName = ug.Guild.Name,
            });
        }

        return Task.FromResult(notifications);
    }
}
