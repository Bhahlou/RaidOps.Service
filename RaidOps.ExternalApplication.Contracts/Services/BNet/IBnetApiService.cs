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

    /// <summary>
    /// Fetches all WoW characters linked to the authenticated BNet account
    /// for the given branch namespace.
    /// </summary>
    /// <param name="accessToken">A valid BNet OAuth2 access token with <c>wow.profile</c> scope.</param>
    /// <param name="region">BNet region code: "us", "eu", "kr", or "tw".</param>
    /// <param name="profileNamespace">
    /// The fully-qualified profile namespace for the target branch
    /// (e.g. <c>"profile-eu"</c>, <c>"profile-classic1x-eu"</c>).
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>All WoW accounts and their characters for the given namespace.</returns>
    Task<BnetWowAccountsResponse> GetWowCharactersAsync(string accessToken, string region, string profileNamespace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the character profile (level, item level, guild) from
    /// <c>GET /profile/wow/character/{realmSlug}/{characterName}</c>.
    /// </summary>
    Task<BnetCharacterDetailResponse> GetCharacterAsync(string accessToken, string region, string profileNamespace, string realmSlug, string characterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the character's media assets (avatar URL) from
    /// <c>GET /profile/wow/character/{realmSlug}/{characterName}/character-media</c>.
    /// </summary>
    Task<BnetCharacterMediaResponse> GetCharacterMediaAsync(string accessToken, string region, string profileNamespace, string realmSlug, string characterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the character's talent specializations from
    /// <c>GET /profile/wow/character/{realmSlug}/{characterName}/specializations</c>.
    /// </summary>
    Task<BnetCharacterSpecializationsResponse> GetCharacterSpecializationsAsync(string accessToken, string region, string profileNamespace, string realmSlug, string characterName, CancellationToken cancellationToken = default);
}
