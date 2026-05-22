using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RaidOps.Application.Contracts.Configuration;
using RaidOps.Application.Contracts.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RaidOps.Application.Implementations.Services;

/// <summary>
/// HMAC-SHA256 JWT implementation of <see cref="IJwtService"/>.
/// Generates and validates access and refresh tokens according to the values
/// supplied in <see cref="JwtSettings"/>.
/// </summary>
public class JwtService(IOptions<JwtSettings> options) : IJwtService
{
    private readonly JwtSettings _settings = options.Value;

    /// <summary>
    /// Generates a short-lived access token containing the user's Discord ID and username.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID stored as the <c>sub</c> claim.</param>
    /// <param name="username">The Discord username stored as the <c>name</c> claim.</param>
    /// <returns>
    /// A tuple of the signed JWT string and its UTC expiration timestamp.
    /// </returns>
    public (string token, DateTime expiry) GenerateAccessToken(string discordId, string username)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, discordId),
            new Claim(JwtRegisteredClaimNames.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        return (BuildToken(claims, expiry), expiry);
    }

    /// <summary>
    /// Generates a long-lived refresh token containing only the user's Discord ID.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID stored as the <c>sub</c> claim.</param>
    /// <returns>
    /// A tuple of the signed JWT string and its UTC expiration timestamp.
    /// </returns>
    public (string token, DateTime expiry) GenerateRefreshToken(string discordId)
    {
        var expiry = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, discordId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        return (BuildToken(claims, expiry), expiry);
    }

    /// <summary>
    /// Validates a refresh token against the configured signing key, issuer, audience,
    /// and lifetime. A clock skew of 30 seconds is applied.
    /// </summary>
    /// <param name="token">The JWT string to validate.</param>
    /// <returns>
    /// The <see cref="ClaimsPrincipal"/> if validation succeeds, or <c>null</c> if the token
    /// is invalid, expired, or tampered with.
    /// </returns>
    public ClaimsPrincipal? ValidateRefreshToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        // Preserve short JWT claim names (e.g. "sub") instead of mapping to long XML URIs.
        // Without this, JwtSecurityTokenHandler maps "sub" → ClaimTypes.NameIdentifier
        // and FindFirst("sub") returns null.
        tokenHandler.InboundClaimTypeMap.Clear();
        var key = Encoding.UTF8.GetBytes(_settings.Key);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            return tokenHandler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds and signs a JWT with the given claims and expiry using HMAC-SHA256.
    /// </summary>
    /// <param name="claims">The claims to embed in the token payload.</param>
    /// <param name="expiry">The UTC expiration time for the token.</param>
    /// <returns>The serialized, signed JWT string.</returns>
    private string BuildToken(Claim[] claims, DateTime expiry)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
