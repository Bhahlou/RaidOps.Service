using RaidOps.Domain.Enums;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for the dismissal ledger backing derived in-app notifications.
/// </summary>
public interface INotificationDismissalRepository
{
    /// <summary>
    /// Returns the set of (type, guild) pairs the given user has dismissed.
    /// </summary>
    /// <param name="userDiscordId">Discord snowflake ID of the user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<HashSet<(NotificationType Type, string GuildId)>> GetDismissedKeysAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the given user dismissed a notification. Idempotent — no-ops if already dismissed.
    /// </summary>
    /// <param name="userDiscordId">Discord snowflake ID of the user.</param>
    /// <param name="type">The kind of notification dismissed.</param>
    /// <param name="guildId">Discord snowflake ID of the guild the notification was scoped to.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task DismissAsync(string userDiscordId, NotificationType type, string guildId, CancellationToken cancellationToken = default);
}
