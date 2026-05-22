namespace RaidOps.Application.Contracts.Configuration;

/// <summary>
/// Strongly-typed configuration settings for JWT token generation and validation,
/// bound from the <c>JwtSettings</c> section of application configuration.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Gets or sets the HMAC-SHA256 signing key used to sign and verify JWT tokens.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the expected issuer (<c>iss</c>) claim value embedded in every token.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the expected audience (<c>aud</c>) claim value embedded in every token.
    /// </summary>
    public required string Audience { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of access tokens in minutes.
    /// Defaults to <c>15</c> minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the lifetime of refresh tokens in days.
    /// Defaults to <c>30</c> days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
