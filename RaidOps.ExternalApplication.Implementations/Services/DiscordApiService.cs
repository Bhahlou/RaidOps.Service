using Microsoft.Extensions.Configuration;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RaidOps.ExternalApplication.Implementations.Services;

/// <summary>
/// HTTP client implementation of <see cref="IDiscordApiService"/> that calls
/// Discord REST API v10 endpoints using a <see cref="HttpClient"/> registered via
/// the typed-client pattern.
/// </summary>
public class DiscordApiService(HttpClient httpClient, IConfiguration configuration) : IDiscordApiService
{
    private const string DiscordApiBase = "https://discord.com/api/v10";

    /// <summary>
    /// Sends a <c>GET /users/@me</c> request to the Discord API and returns the
    /// authenticated user's profile.
    /// </summary>
    /// <param name="accessToken">A valid Discord OAuth2 access token used as the Bearer credential.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The current user's Discord profile.</returns>
    /// <exception cref="HttpRequestException">Thrown when the Discord API returns a non-success status code.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be deserialized.</exception>
    public async Task<GetDiscordUserInfoResponse> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}/users/@me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<GetDiscordUserInfoResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize Discord user info.");
    }

    /// <summary>
    /// Sends a <c>GET /users/@me/guilds</c> request to the Discord API and returns
    /// all guilds the authenticated user belongs to.
    /// </summary>
    /// <param name="accessToken">A valid Discord OAuth2 access token used as the Bearer credential.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A list of the user's guilds, or an empty list if the response body is <c>null</c>.
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown when the Discord API returns a non-success status code.</exception>
    public async Task<List<GetDiscordUserGuildResponse>> GetCurrentUserGuildsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}/users/@me/guilds");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<GetDiscordUserGuildResponse>>(content)
            ?? [];
    }

    /// <summary>
    /// Exchanges a Discord OAuth2 refresh token for a new access/refresh token pair by
    /// posting to <c>POST /oauth2/token</c> using the application's client credentials
    /// read from <c>Discord:ClientId</c> and <c>Discord:ClientSecret</c> in configuration.
    /// </summary>
    /// <param name="refreshToken">The Discord OAuth2 refresh token to exchange.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The new Discord token pair and expiry information.</returns>
    /// <exception cref="HttpRequestException">Thrown when the Discord API returns a non-success status code.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be deserialized.</exception>
    public async Task<RefreshDiscordTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var clientId = configuration["Discord:ClientId"]!;
        var clientSecret = configuration["Discord:ClientSecret"]!;

        var body = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
        ]);

        var response = await httpClient.PostAsync($"{DiscordApiBase}/oauth2/token", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<RefreshDiscordTokenResponse>(content)
            ?? throw new InvalidOperationException("Failed to deserialize Discord token response.");
    }
}
