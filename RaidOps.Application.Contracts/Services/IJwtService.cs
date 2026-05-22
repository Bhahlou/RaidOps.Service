using System.Security.Claims;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Provides operations for generating and validating JWT access and refresh tokens.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a short-lived JWT access token for the specified user.
    /// </summary>
    /// <param name="discordId">The user's Discord snowflake ID, stored as the <c>sub</c> claim.</param>
    /// <param name="username">The user's Discord username, stored as the <c>name</c> claim.</param>
    /// <returns>
    /// A tuple containing the signed JWT string and its UTC expiration timestamp.
    /// </returns>
    (string token, DateTime expiry) GenerateAccessToken(string discordId, string username);

    /// <summary>
    /// Generates a long-lived JWT refresh token for the specified user.
    /// </summary>
    /// <param name="discordId">The user's Discord snowflake ID, stored as the <c>sub</c> claim.</param>
    /// <returns>
    /// A tuple containing the signed JWT string and its UTC expiration timestamp.
    /// </returns>
    (string token, DateTime expiry) GenerateRefreshToken(string discordId);

    /// <summary>
    /// Validates a refresh token and returns the associated claims principal if the token is valid.
    /// </summary>
    /// <param name="token">The JWT refresh token string to validate.</param>
    /// <returns>
    /// The <see cref="ClaimsPrincipal"/> extracted from the token, or <c>null</c> if the token
    /// is expired, tampered with, or otherwise invalid.
    /// </returns>
    ClaimsPrincipal? ValidateRefreshToken(string token);
}
