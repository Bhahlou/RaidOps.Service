using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;

/// <summary>
/// Represents the token response returned by the Discord OAuth2 token endpoint
/// after exchanging a refresh token for a new access token.
/// </summary>
public class RefreshDiscordTokenResponse
{
    /// <summary>Gets or sets the new short-lived Discord OAuth2 access token.</summary>
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }

    /// <summary>Gets or sets the new long-lived Discord OAuth2 refresh token.</summary>
    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds until the new access token expires,
    /// as reported by Discord.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
