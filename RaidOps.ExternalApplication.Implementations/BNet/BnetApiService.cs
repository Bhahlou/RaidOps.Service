using Microsoft.Extensions.Configuration;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

namespace RaidOps.ExternalApplication.Implementations.BNet;

/// <summary>
/// HTTP client implementation of <see cref="IBnetApiService"/> that calls
/// the Battle.net OAuth2 and profile API endpoints.
/// Base URLs are constructed per-region at call time.
/// </summary>
public class BnetApiService(HttpClient httpClient, IConfiguration configuration) : IBnetApiService
{
    private static string BnetBase(string region)    => $"https://{region}.battle.net";
    private static string BnetApiBase(string region) => $"https://{region}.api.blizzard.com";

    /// <summary>
    /// Builds the Battle.net OAuth2 authorization URL for the given region.
    /// Scope is fixed to <c>wow.profile</c>.
    /// </summary>
    public string BuildAuthorizationUrl(string region, string redirectUri, string state)
    {
        var clientId = configuration["BattleNet:ClientId"]!;
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = "wow.profile";
        query["state"] = state;
        return $"{BnetBase(region)}/oauth/authorize?{query}";
    }

    /// <summary>
    /// Exchanges an OAuth2 authorization code for a token pair by posting to
    /// <c>POST https://{region}.battle.net/oauth/token</c> using HTTP Basic auth
    /// (client_id + client_secret) and the standard authorization_code grant.
    /// </summary>
    public async Task<BnetTokenResponse> ExchangeCodeAsync(
        string code,
        string redirectUri,
        string region,
        CancellationToken cancellationToken = default)
    {
        var clientId = configuration["BattleNet:ClientId"]!;
        var clientSecret = configuration["BattleNet:ClientSecret"]!;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BnetBase(region)}/oauth/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri),
        ]);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<BnetTokenResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize BNet token response.");
    }

    /// <summary>
    /// Fetches the authenticated user's BNet profile (BattleTag + account ID) from
    /// <c>GET https://{region}.battle.net/oauth/userinfo</c>.
    /// </summary>
    public async Task<BnetUserInfoResponse> GetUserInfoAsync(
        string accessToken,
        string region,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BnetBase(region)}/oauth/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<BnetUserInfoResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize BNet user info response.");
    }

    /// <summary>
    /// Fetches all WoW characters for the authenticated account from
    /// <c>GET https://{region}.api.blizzard.com/profile/user/wow</c>.
    /// The <paramref name="profileNamespace"/> determines which branch is queried
    /// (e.g. <c>"profile-eu"</c> for Retail, <c>"profile-classic1x-eu"</c> for Classic Era).
    /// </summary>
    public async Task<BnetWowAccountsResponse> GetWowCharactersAsync(
        string accessToken,
        string region,
        string profileNamespace,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BnetApiBase(region)}/profile/user/wow?namespace={profileNamespace}&locale=en_US";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<BnetWowAccountsResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize BNet WoW accounts response.");
    }

    /// <inheritdoc/>
    public async Task<string> GetAppTokenAsync(string region, CancellationToken cancellationToken = default)
    {
        var clientId     = configuration["BattleNet:ClientId"]!;
        var clientSecret = configuration["BattleNet:ClientSecret"]!;
        var credentials  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BnetBase(region)}/oauth/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        ]);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var token = JsonSerializer.Deserialize<BnetTokenResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize BNet app token response.");
        return token.AccessToken;
    }

    /// <inheritdoc/>
    public async Task<BnetCharacterDetailResponse> GetCharacterAsync(
        string accessToken, string region, string profileNamespace,
        string realmSlug, string characterName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BnetApiBase(region)}/profile/wow/character/{realmSlug}/{characterName.ToLowerInvariant()}?namespace={profileNamespace}&locale=en_US";
        return await GetProfileAsync<BnetCharacterDetailResponse>(accessToken, url, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<BnetCharacterMediaResponse> GetCharacterMediaAsync(
        string accessToken, string region, string profileNamespace,
        string realmSlug, string characterName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BnetApiBase(region)}/profile/wow/character/{realmSlug}/{characterName.ToLowerInvariant()}/character-media?namespace={profileNamespace}&locale=en_US";
        return await GetProfileAsync<BnetCharacterMediaResponse>(accessToken, url, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<BnetCharacterSpecializationsResponse> GetCharacterSpecializationsAsync(
        string accessToken, string region, string profileNamespace,
        string realmSlug, string characterName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BnetApiBase(region)}/profile/wow/character/{realmSlug}/{characterName.ToLowerInvariant()}/specializations?namespace={profileNamespace}&locale=en_US";
        return await GetProfileAsync<BnetCharacterSpecializationsResponse>(accessToken, url, cancellationToken);
    }

    private async Task<T> GetProfileAsync<T>(string accessToken, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(content)
            ?? throw new InvalidOperationException($"Failed to deserialize BNet response for {url}.");
    }
}
