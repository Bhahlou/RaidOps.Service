using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Services;

/// <summary>
/// Implements <see cref="IDiscordSyncService"/> by coordinating calls to the Discord API
/// and the persistence layer to keep user profiles and guild memberships in sync.
/// </summary>
public class DiscordSyncService(
    IDiscordApiService discordApiService,
    IUsersRepository usersRepository,
    IGuildsRepository guildsRepository,
    IUserGuildsRepository userGuildsRepository) : IDiscordSyncService
{
    /// <summary>
    /// Syncs the user's profile and guild memberships using Discord OAuth2 tokens already
    /// in hand (signup flow). User and guild data are fetched in parallel.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user being synced.</param>
    /// <param name="accessToken">A valid Discord OAuth2 access token for the user.</param>
    /// <param name="refreshToken">The Discord OAuth2 refresh token to persist for future syncs.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The created or updated <see cref="User"/> entity.</returns>
    public async Task<User> SyncUserAndGuildsAsync(string discordId, string accessToken, string refreshToken, CancellationToken cancellationToken = default)
    {
        var (userInfo, discordGuilds) = await FetchDiscordDataAsync(accessToken, cancellationToken);

        var user = await SyncUserAsync(discordId, userInfo.Username, userInfo.Avatar, refreshToken, cancellationToken);
        await SyncGuildsAsync(discordId, discordGuilds, cancellationToken);
        return user;
    }

    /// <summary>
    /// Re-syncs a user's profile and guild memberships using the Discord refresh token
    /// stored in the database (token-refresh flow).
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user to re-sync.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The updated <see cref="User"/> entity.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no user with the given <paramref name="discordId"/> exists in the database.
    /// </exception>
    public async Task<User> SyncUserAndGuildsAsync(string discordId, CancellationToken cancellationToken = default)
    {
        var existing = await usersRepository.GetByDiscordIdAsync(discordId, cancellationToken)
            ?? throw new InvalidOperationException($"User {discordId} not found.");

        var tokenResponse = await discordApiService.RefreshAccessTokenAsync(existing.RefreshToken, cancellationToken);

        var (userInfo, discordGuilds) = await FetchDiscordDataAsync(tokenResponse.AccessToken, cancellationToken);

        var user = await SyncUserAsync(discordId, userInfo.Username, userInfo.Avatar, tokenResponse.RefreshToken, cancellationToken);
        await SyncGuildsAsync(discordId, discordGuilds, cancellationToken);
        return user;
    }

    /// <summary>
    /// Fetches the current user's profile and guild list from the Discord API in parallel.
    /// </summary>
    /// <param name="accessToken">A valid Discord OAuth2 access token.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the user info response and the list of guild responses.
    /// </returns>
    private async Task<(GetDiscordUserInfoResponse userInfo, List<GetDiscordUserGuildResponse> guilds)>
        FetchDiscordDataAsync(string accessToken, CancellationToken cancellationToken)
    {
        var userInfoTask = discordApiService.GetCurrentUserAsync(accessToken, cancellationToken);
        var guildsTask = discordApiService.GetCurrentUserGuildsAsync(accessToken, cancellationToken);
        await Task.WhenAll(userInfoTask, guildsTask);
        return (userInfoTask.Result, guildsTask.Result);
    }

    /// <summary>
    /// Creates a new <see cref="User"/> record or updates the name, avatar hash, refresh token,
    /// and last-refresh timestamp on an existing one.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user.</param>
    /// <param name="name">The user's current Discord display name.</param>
    /// <param name="avatarHash">The user's Discord avatar hash, or <c>null</c> if not set.</param>
    /// <param name="refreshToken">The latest Discord OAuth2 refresh token to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The saved or updated <see cref="User"/> entity.</returns>
    private async Task<User> SyncUserAsync(string discordId, string name, string? avatarHash, string refreshToken, CancellationToken cancellationToken)
    {
        var user = await usersRepository.GetByDiscordIdAsync(discordId, cancellationToken);

        if (user == null)
        {
            user = new User
            {
                DiscordId = discordId,
                Name = name,
                AvatarHash = avatarHash,
                RefreshToken = refreshToken,
                LastRefresh = DateTimeOffset.UtcNow
            };
            return await usersRepository.AddAsync(user, cancellationToken);
        }

        user.Name = name;
        user.AvatarHash = avatarHash;
        user.RefreshToken = refreshToken;
        user.LastRefresh = DateTimeOffset.UtcNow;
        return await usersRepository.UpdateAsync(user, cancellationToken);
    }

    /// <summary>
    /// Upserts guild master data and atomically replaces the user's guild memberships
    /// with the current snapshot from Discord.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user whose memberships are being synced.</param>
    /// <param name="discordGuilds">The list of guilds returned by the Discord API.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    private async Task SyncGuildsAsync(string discordId, List<GetDiscordUserGuildResponse> discordGuilds, CancellationToken cancellationToken)
    {
        // 1. Upsert guild master data (name + iconHash only on existing)
        var guilds = discordGuilds.Select(g => new Guild
        {
            Id = g.Id,
            Name = g.Name,
            IconHash = g.Icon
        }).ToList();

        await guildsRepository.UpsertRangeAsync(guilds, cancellationToken);

        // 2. Replace this user's guild memberships
        var userGuilds = discordGuilds.Select(g => new UserGuild
        {
            UserDiscordId = discordId,
            GuildId = g.Id,
            IsAdmin = g.IsAdmin,
            IsOwner = g.Owner
        }).ToList();

        await userGuildsRepository.ReplaceUserGuildsAsync(discordId, userGuilds, cancellationToken);
    }
}
