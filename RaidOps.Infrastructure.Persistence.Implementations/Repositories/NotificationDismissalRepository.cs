using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationDismissalRepository"/>.
/// </summary>
public class NotificationDismissalRepository(RaidOpsDbContext context) : INotificationDismissalRepository
{
    /// <summary>
    /// Returns the set of (type, guild) pairs the given user has dismissed.
    /// </summary>
    public async Task<HashSet<(NotificationType Type, string GuildId)>> GetDismissedKeysAsync(string userDiscordId, CancellationToken cancellationToken = default)
    {
        var dismissals = await context.NotificationDismissals
            .Where(nd => nd.UserDiscordId == userDiscordId)
            .Select(nd => new { nd.Type, nd.GuildId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. dismissals.Select(d => (d.Type, d.GuildId))];
    }

    /// <summary>
    /// Records a dismissal if one doesn't already exist for this (user, type, guild) combination.
    /// </summary>
    public async Task DismissAsync(string userDiscordId, NotificationType type, string guildId, CancellationToken cancellationToken = default)
    {
        var alreadyDismissed = await context.NotificationDismissals
            .AsNoTracking()
            .AnyAsync(nd => nd.UserDiscordId == userDiscordId && nd.Type == type && nd.GuildId == guildId, cancellationToken);

        if (alreadyDismissed)
            return;

        context.NotificationDismissals.Add(new NotificationDismissal
        {
            UserDiscordId = userDiscordId,
            Type = type,
            GuildId = guildId,
            DismissedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
