using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Contracts.Services;

public interface IDiscordSyncService
{
    /// <summary>
    /// Syncs user info and guilds using a Discord access token already in hand (signup flow).
    /// </summary>
    Task<User> SyncUserAndGuildsAsync(string discordId, string accessToken, string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-fetches a Discord access token from the stored refresh token, then syncs (refresh-token flow).
    /// </summary>
    Task<User> SyncUserAndGuildsAsync(string discordId, CancellationToken cancellationToken = default);
}
