using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RaidOps.Application.Contracts.Configuration;
using RaidOps.Application.Implementations.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RaidOps.UnitTests.Application.Services;

public class JwtServiceTests
{
    private readonly JwtService _sut;
    private readonly JwtSettings _settings = new()
    {
        Key      = "test-secret-key-that-is-long-enough-for-hmac256",
        Issuer   = "RaidOps.Test",
        Audience = "RaidOps.Test",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays   = 30,
    };

    public JwtServiceTests()
    {
        _sut = new JwtService(Options.Create(_settings));
    }

    // ── Access token ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateAccessToken_ContainsSubAndNameClaims()
    {
        var (token, _) = _sut.GenerateAccessToken("123456", "Bhahlou");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "sub"  && c.Value == "123456");
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Bhahlou");
    }

    [Fact]
    public void GenerateAccessToken_ExpiryMatchesSettings()
    {
        var before     = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);
        var (_, expiry) = _sut.GenerateAccessToken("123456", "Bhahlou");
        var after      = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        expiry.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // ── Refresh token ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateRefreshToken_ContainsSubClaim()
    {
        var (token, _) = _sut.GenerateRefreshToken("123456");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == "123456");
    }

    [Fact]
    public void ValidateRefreshToken_ValidToken_ReturnsPrincipal()
    {
        var (token, _) = _sut.GenerateRefreshToken("123456");

        var principal = _sut.ValidateRefreshToken(token);

        principal.Should().NotBeNull();
        principal!.FindFirst("sub")!.Value.Should().Be("123456");
    }

    [Fact]
    public void ValidateRefreshToken_TamperedToken_ReturnsNull()
    {
        var (token, _) = _sut.GenerateRefreshToken("123456");
        var tampered   = token[..^5] + "XXXXX";

        _sut.ValidateRefreshToken(tampered).Should().BeNull();
    }

    [Fact]
    public void ValidateRefreshToken_WrongKey_ReturnsNull()
    {
        var otherService = new JwtService(Options.Create(new JwtSettings
        {
            Key = "different-secret-key-also-long-enough-for-hmac", Issuer = _settings.Issuer, Audience = _settings.Audience
        }));
        var (token, _) = otherService.GenerateRefreshToken("123456");

        _sut.ValidateRefreshToken(token).Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds a signed JWT with exactly the given claims, bypassing JwtService.</summary>
    private string BuildCustomToken(params Claim[] claims)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Discord state token ───────────────────────────────────────────────────

    [Fact]
    public void ValidateStateToken_ValidToken_ReturnsGuildAndDiscordId()
    {
        var token = _sut.GenerateStateToken(guildId: "guild-99", discordId: "user-42");

        var result = _sut.ValidateStateToken(token);

        result.Should().NotBeNull();
        result!.Value.GuildId.Should().Be("guild-99");
        result!.Value.DiscordId.Should().Be("user-42");
    }

    [Fact]
    public void ValidateStateToken_TamperedToken_ReturnsNull()
    {
        var token    = _sut.GenerateStateToken("guild-99", "user-42");
        var tampered = token[..^5] + "XXXXX";

        _sut.ValidateStateToken(tampered).Should().BeNull();
    }

    [Fact]
    public void ValidateStateToken_WithReturnTo_RoundTripsReturnTo()
    {
        var token = _sut.GenerateStateToken(guildId: "guild-99", discordId: "user-42", returnTo: "get-started");

        var result = _sut.ValidateStateToken(token);

        result!.Value.ReturnTo.Should().Be("get-started");
    }

    [Fact]
    public void ValidateStateToken_WithoutReturnTo_ReturnToIsNull()
    {
        var token = _sut.GenerateStateToken(guildId: "guild-99", discordId: "user-42");

        var result = _sut.ValidateStateToken(token);

        result!.Value.ReturnTo.Should().BeNull();
    }

    [Fact]
    public void ValidateStateToken_BnetTokenMissingGldClaim_ReturnsNull()
    {
        // A BNet state token is valid (correct key/issuer) but has no "gld" claim.
        var token = _sut.GenerateBnetStateToken(discordId: "user-42", region: "eu");

        _sut.ValidateStateToken(token).Should().BeNull();
    }

    [Fact]
    public void ValidateStateToken_ValidTokenWithNoSubClaim_ReturnsNull()
    {
        // Valid signature/issuer/audience but no "sub" claim → discordId is null.
        var token = BuildCustomToken(new Claim("gld", "guild-99"));

        _sut.ValidateStateToken(token).Should().BeNull();
    }

    // ── BNet state token ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateBnetStateToken_ValidToken_ReturnsDiscordIdAndRegion()
    {
        var token = _sut.GenerateBnetStateToken(discordId: "user-42", region: "eu");

        var result = _sut.ValidateBnetStateToken(token);

        result.Should().NotBeNull();
        result!.Value.DiscordId.Should().Be("user-42");
        result!.Value.Region.Should().Be("eu");
    }

    [Fact]
    public void ValidateBnetStateToken_TamperedToken_ReturnsNull()
    {
        var token    = _sut.GenerateBnetStateToken("user-42", "eu");
        var tampered = token[..^5] + "XXXXX";

        _sut.ValidateBnetStateToken(tampered).Should().BeNull();
    }

    [Fact]
    public void ValidateBnetStateToken_TokenSignedForDiscordFlow_ReturnsNull()
    {
        // A Discord state token has "gld" but no "rgn" → region is null → returns null.
        var token = _sut.GenerateStateToken(guildId: "guild-99", discordId: "user-42");

        _sut.ValidateBnetStateToken(token).Should().BeNull();
    }

    [Fact]
    public void ValidateBnetStateToken_ValidTokenWithNoSubClaim_ReturnsNull()
    {
        // Valid signature/issuer/audience but no "sub" claim → discordId is null.
        var token = BuildCustomToken(new Claim("rgn", "eu"));

        _sut.ValidateBnetStateToken(token).Should().BeNull();
    }
}
