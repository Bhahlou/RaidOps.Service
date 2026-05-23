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
    private static string BnetBase(string region) => $"https://{region}.battle.net";

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
}
