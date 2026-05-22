using RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;

namespace RaidOps.ExternalApplication.Contracts.Services.Discord;

/// <summary>
/// Abstraction over the Discord REST API v10 calls required by RaidOps.
/// </summary>
public interface IDiscordApiService
{
    /// <summary>
    /// Retrieves the profile of the currently authenticated Discord user.
    /// </summary>
    /// <param name="accessToken">A valid Discord OAuth2 access token.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The user's Discord profile information.</returns>
    Task<GetDiscordUserInfoResponse> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of Discord guilds the authenticated user belongs to.
    /// </summary>
    /// <param name="accessToken">A valid Discord OAuth2 access token.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>A list of guilds the user is a member of.</returns>
    Task<List<GetDiscordUserGuildResponse>> GetCurrentUserGuildsAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a Discord OAuth2 refresh token for a new access/refresh token pair
    /// using the application's client credentials.
    /// </summary>
    /// <param name="refreshToken">The Discord OAuth2 refresh token to exchange.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The new Discord token pair and the access token lifetime in seconds.</returns>
    Task<RefreshDiscordTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
