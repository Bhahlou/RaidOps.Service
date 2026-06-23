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
    public ClaimsPrincipal? ValidateRefreshToken(string token) => TryValidate(token);

    /// <summary>
    /// Generates a short-lived CSRF state token for the Discord bot OAuth2 registration flow.
    /// Embeds the target guild ID and the initiating user's Discord ID, expiring after 10 minutes.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild being registered.</param>
    /// <param name="discordId">The Discord snowflake ID of the user initiating the registration.</param>
    /// <param name="returnTo">Optional discriminator for where the front end should land afterward.</param>
    /// <returns>A signed JWT string to be passed as the OAuth2 <c>state</c> parameter.</returns>
    public string GenerateStateToken(string guildId, string discordId, string? returnTo = null)
    {
        var expiry = DateTime.UtcNow.AddMinutes(10);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, discordId),
            new("gld", guildId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (returnTo != null)
            claims.Add(new Claim("rtn", returnTo));

        return BuildToken(claims.ToArray(), expiry);
    }

    /// <summary>
    /// Validates a state token and extracts the embedded guild and user identifiers.
    /// Returns <c>null</c> if the token is invalid, expired, or tampered with.
    /// </summary>
    /// <param name="token">The JWT state token to validate.</param>
    /// <returns>
    /// A tuple of <c>(GuildId, DiscordId, ReturnTo)</c> on success, or <c>null</c> on failure.
    /// </returns>
    public (string GuildId, string DiscordId, string? ReturnTo)? ValidateStateToken(string token)
    {
        var principal = TryValidate(token);
        var discordId = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var guildId   = principal?.FindFirst("gld")?.Value;
        var returnTo  = principal?.FindFirst("rtn")?.Value;
        return discordId is null || guildId is null ? null : (guildId, discordId, returnTo);
    }

    /// <summary>
    /// Generates a short-lived CSRF state token for the Battle.net OAuth2 link flow.
    /// Embeds the user's Discord ID and their chosen BNet region, expiring after 10 minutes.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user initiating the BNet link.</param>
    /// <param name="region">BNet region code ("us", "eu", "kr", "tw").</param>
    /// <returns>A signed JWT string to be passed as the OAuth2 <c>state</c> parameter.</returns>
    public string GenerateBnetStateToken(string discordId, string region)
    {
        var expiry = DateTime.UtcNow.AddMinutes(10);
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, discordId),
            new("rgn", region),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];
        return BuildToken(claims, expiry);
    }

    /// <summary>
    /// Validates a BNet state token and extracts the embedded Discord ID and region.
    /// Returns <c>null</c> if the token is invalid, expired, or tampered with.
    /// </summary>
    /// <param name="token">The JWT state token to validate.</param>
    /// <returns>
    /// A tuple of <c>(DiscordId, Region)</c> on success, or <c>null</c> on failure.
    /// </returns>
    public (string DiscordId, string Region)? ValidateBnetStateToken(string token)
    {
        var principal = TryValidate(token);
        var discordId = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var region    = principal?.FindFirst("rgn")?.Value;
        return discordId is null || region is null ? null : (discordId, region);
    }

    /// <summary>
    /// Validates a JWT against the configured key, issuer, audience, and lifetime.
    /// Preserves short claim names (e.g. "sub") by clearing the inbound claim type map.
    /// Returns <c>null</c> if validation fails for any reason.
    /// </summary>
    private ClaimsPrincipal? TryValidate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _settings.Issuer,
            ValidAudience            = _settings.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            ClockSkew                = TimeSpan.FromSeconds(30)
        };
        try { return handler.ValidateToken(token, parameters, out _); }
        catch { return null; }
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
