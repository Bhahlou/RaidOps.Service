using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet;

/// <summary>
/// Abstraction over the Battle.net OAuth2 and profile API calls required by RaidOps.
/// All operations are region-scoped because Blizzard hosts separate API endpoints per region.
/// </summary>
public interface IBnetApiService
{
    /// <summary>
    /// Builds the Battle.net OAuth2 authorization URL to which the user should be redirected.
    /// </summary>
    /// <param name="region">BNet region code: "us", "eu", "kr", or "tw".</param>
    /// <param name="redirectUri">The callback URI registered in the Battle.net developer portal.</param>
    /// <param name="state">CSRF state token to embed in the redirect URL.</param>
    /// <returns>The fully-formed authorization URL.</returns>
    string BuildAuthorizationUrl(string region, string redirectUri, string state);

    /// <summary>
    /// Exchanges an OAuth2 authorization code for an access/refresh token pair.
    /// </summary>
    /// <param name="code">The authorization code returned by Battle.net.</param>
    /// <param name="redirectUri">The same redirect URI used when the authorization was initiated.</param>
    /// <param name="region">BNet region code: "us", "eu", "kr", or "tw".</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The token pair and expiry information.</returns>
    Task<BnetTokenResponse> ExchangeCodeAsync(string code, string redirectUri, string region, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the Battle.net user profile (BattleTag, account ID) using a valid access token.
    /// </summary>
    /// <param name="accessToken">A valid BNet OAuth2 access token.</param>
    /// <param name="region">BNet region code: "us", "eu", "kr", or "tw".</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The user's BNet profile.</returns>
    Task<BnetUserInfoResponse> GetUserInfoAsync(string accessToken, string region, CancellationToken cancellationToken = default);
}
