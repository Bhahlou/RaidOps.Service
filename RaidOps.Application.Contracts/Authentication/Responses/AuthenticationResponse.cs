namespace RaidOps.Application.Contracts.Authentication.Responses;

/// <summary>
/// Payload returned after a successful authentication or token-refresh operation,
/// containing both the short-lived access token and the long-lived refresh token.
/// </summary>
public class AuthenticationResponse
{
    /// <summary>
    /// Gets or sets the signed JWT access token used to authenticate API requests.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the signed JWT refresh token used to obtain a new access token when it expires.
    /// </summary>
    public required string RefreshToken { get; set; }

    /// <summary>G
    /// ets or sets the UTC date and time at which the access token expires.
    /// </summary>
    public DateTime AccessTokenExpiration { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time at which the refresh token expires.
    /// </summary>
    public DateTime RefreshTokenExpiration { get; set; }
}
