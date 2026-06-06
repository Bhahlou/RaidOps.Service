using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Generates signed JWT tokens for integration tests using the shared test signing key.
/// </summary>
public static class TestTokenBuilder
{
    public const string JwtKey = "integration-test-signing-key-must-be-at-least-32-chars!";
    public const string JwtIssuer = "raidops-integration-tests";
    public const string JwtAudience = "raidops-integration-client";

    /// <summary>
    /// Creates a valid access token for the given Discord user, signed with the test key.
    /// </summary>
    public static string CreateAccessToken(string discordId = "123456789012345678", string username = "TestUser")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, discordId),
            new Claim(JwtRegisteredClaimNames.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        return BuildToken(claims, DateTime.UtcNow.AddHours(1));
    }

    /// <summary>
    /// Creates a valid refresh token for the given Discord user, signed with the test key.
    /// </summary>
    public static string CreateRefreshToken(string discordId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, discordId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        return BuildToken(claims, DateTime.UtcNow.AddDays(30));
    }

    /// <summary>
    /// Creates a signed BNet state token for the Battle.net OAuth2 callback flow.
    /// Mirrors JwtService.GenerateBnetStateToken using the test signing key.
    /// </summary>
    public static string CreateBnetStateToken(string discordId, string region)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, discordId),
            new Claim("rgn", region),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        return BuildToken(claims, DateTime.UtcNow.AddMinutes(10));
    }

    /// <summary>
    /// Creates a signed state token for the guild registration OAuth2 flow.
    /// Mirrors JwtService.GenerateStateToken using the test signing key.
    /// </summary>
    public static string CreateStateToken(string guildId, string discordId)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, discordId),
            new Claim("gld", guildId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        return BuildToken(claims, DateTime.UtcNow.AddMinutes(10));
    }

    private static string BuildToken(Claim[] claims, DateTime expiry)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
