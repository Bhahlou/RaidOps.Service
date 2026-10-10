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
    private const string TypeClaim = "typ";
    private const string AccessTokenType = "access";
    private const string RefreshTokenType = "refresh";
    private const string GuildRegStateTokenType = "state_guild_reg";
    private const string BnetStateTokenType = "state_bnet";

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
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(TypeClaim, AccessTokenType)
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
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(TypeClaim, RefreshTokenType)
        };
        return (BuildToken(claims, expiry), expiry);
    }

    /// <summary>
    /// Validates a refresh token against the configured signing key, issuer, audience,
    /// lifetime, and expected <c>typ</c> claim. A clock skew of 30 seconds is applied.
    /// Rejects tokens of any other RaidOps-issued type (access, state), even if otherwise
    /// validly signed, to prevent cross-purpose token replay.
    /// </summary>
    /// <param name="token">The JWT string to validate.</param>
    /// <returns>
    /// The <see cref="ClaimsPrincipal"/> if validation succeeds, or <c>null</c> if the token
    /// is invalid, expired, tampered with, or not a refresh token.
    /// </returns>
    public ClaimsPrincipal? ValidateRefreshToken(string token) => TryValidate(token, RefreshTokenType);

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
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TypeClaim, GuildRegStateTokenType)
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
        var principal = TryValidate(token, GuildRegStateTokenType);
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
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TypeClaim, BnetStateTokenType)
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
        var principal = TryValidate(token, BnetStateTokenType);
        var discordId = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var region    = principal?.FindFirst("rgn")?.Value;
        return discordId is null || region is null ? null : (discordId, region);
    }

    /// <summary>
    /// Validates a JWT against the configured key, issuer, audience, and lifetime, then checks
    /// that its <c>typ</c> claim matches <paramref name="expectedType"/>. All RaidOps-issued
    /// tokens (access, refresh, state) share the same signing key/issuer/audience, so the
    /// <c>typ</c> claim is what prevents one token purpose from being replayed as another
    /// (e.g. a leaked OAuth <c>state</c> token being used as a refresh token).
    /// Preserves short claim names (e.g. "sub") by clearing the inbound claim type map.
    /// Returns <c>null</c> if validation fails for any reason, including a <c>typ</c> mismatch.
    /// </summary>
    private ClaimsPrincipal? TryValidate(string token, string expectedType)
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
        try
        {
            var principal = handler.ValidateToken(token, parameters, out _);
            var type = principal.FindFirst(TypeClaim)?.Value;
            return type == expectedType ? principal : null;
        }
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
