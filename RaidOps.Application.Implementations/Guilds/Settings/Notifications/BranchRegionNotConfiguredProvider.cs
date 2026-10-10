using RaidOps.Application.Contracts.Notifications.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Implementations.Guilds.Settings.Notifications;

/// <summary>
/// Surfaces <see cref="NotificationType.BranchRegionNotConfigured"/> for admins of a guild that has
/// at least one active <see cref="GuildBranch"/> with no <see cref="GuildBranch.Region"/> set yet.
/// Fires once per guild (not per branch) — the front's branches settings tab is where the admin
/// sees exactly which branch still needs it. Never fires for non-admins.
/// </summary>
public class BranchRegionNotConfiguredProvider : INotificationSignalProvider
{
    /// <inheritdoc/>
    public Task<List<NotificationResponse>> GetActiveAsync(
        string discordId, IReadOnlyList<UserGuild> eligibleGuilds, CancellationToken cancellationToken = default)
    {
        var notifications = new List<NotificationResponse>();
        foreach (var ug in eligibleGuilds)
        {
            if (!ug.IsAdmin || !ug.Guild.IsRegistered)
                continue;

            var activeBranches = ug.Guild.Branches.Where(b => b.IsActive).ToList();
            if (activeBranches.Count == 0)
                continue;

            if (activeBranches.All(b => !string.IsNullOrWhiteSpace(b.Region)))
                continue;

            notifications.Add(new NotificationResponse
            {
                Type = NotificationType.BranchRegionNotConfigured,
                GuildId = ug.GuildId,
                GuildName = ug.Guild.Name,
            });
        }

        return Task.FromResult(notifications);
    }
}
