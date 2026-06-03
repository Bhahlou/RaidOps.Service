using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>
/// Response returned by the Battle.net OAuth2 token endpoint
/// (<c>POST https://{region}.battle.net/oauth/token</c>).
/// </summary>
public class BnetTokenResponse
{
    /// <summary>OAuth2 access token used to call BNet APIs on behalf of the user.</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Token type — always "bearer" for BNet OAuth2.</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    /// <summary>Number of seconds until the access token expires.</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// OAuth2 refresh token. May be absent depending on the BNet OAuth2 flow version.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>Granted scopes (e.g. "wow.profile").</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}
