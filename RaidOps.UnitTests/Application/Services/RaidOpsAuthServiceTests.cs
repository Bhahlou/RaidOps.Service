using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Services;
using RaidOps.Domain.Models.Discord;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.UnitTests.Application.Services;

public class RaidOpsAuthServiceTests
{
    private readonly Mock<IDiscordSyncService> _discordSync = new();
    private readonly Mock<IJwtService>         _jwt         = new();
    private readonly RaidOpsAuthService        _sut;

    private static readonly User FakeUser = new() { DiscordId = "123", Name = "Bhahlou" };
    private const string FakeAccessToken  = "access-jwt";
    private const string FakeRefreshToken = "refresh-jwt";

    public RaidOpsAuthServiceTests()
    {
        _sut = new RaidOpsAuthService(_discordSync.Object, _jwt.Object, NullLogger<RaidOpsAuthService>.Instance);

        _jwt.Setup(j => j.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((FakeAccessToken, DateTime.UtcNow.AddMinutes(15)));
        _jwt.Setup(j => j.GenerateRefreshToken(It.IsAny<string>()))
            .Returns((FakeRefreshToken, DateTime.UtcNow.AddDays(30)));
    }

    // ── Signup ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleSignupAsync_Success_ReturnsOkWithTokens()
    {
        var command = new SignupCommand
        {
            DiscordId = "123", DiscordAccessToken = "discord-access", DiscordRefreshToken = "discord-refresh"
        };
        _discordSync.Setup(s => s.SyncUserAndGuildsAsync("123", "discord-access", "discord-refresh", default))
            .ReturnsAsync(FakeUser);

        var result = await _sut.HandleSignupAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be(FakeAccessToken);
        result.Value.RefreshToken.Should().Be(FakeRefreshToken);
    }

    [Fact]
    public async Task HandleSignupAsync_SyncThrows_ReturnsFailWithMessage()
    {
        var command = new SignupCommand
        {
            DiscordId = "123", DiscordAccessToken = "access", DiscordRefreshToken = "refresh"
        };
        _discordSync.Setup(s => s.SyncUserAndGuildsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new Exception("Discord API unavailable"));

        var result = await _sut.HandleSignupAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be("Discord API unavailable");
    }

    [Fact]
    public async Task HandleSignupAsync_GeneratesTokensForSyncedUser()
    {
        var command = new SignupCommand
        {
            DiscordId = "123", DiscordAccessToken = "access", DiscordRefreshToken = "refresh"
        };
        _discordSync.Setup(s => s.SyncUserAndGuildsAsync("123", "access", "refresh", default))
            .ReturnsAsync(FakeUser);

        await _sut.HandleSignupAsync(command);

        _jwt.Verify(j => j.GenerateAccessToken("123", "Bhahlou"), Times.Once);
        _jwt.Verify(j => j.GenerateRefreshToken("123"), Times.Once);
    }

    // ── Refresh token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleRefreshTokenAsync_InvalidToken_ReturnsFail()
    {
        _jwt.Setup(j => j.ValidateRefreshToken("bad-token")).Returns((ClaimsPrincipal?)null);

        var result = await _sut.HandleRefreshTokenAsync(new RefreshTokenCommand { RefreshToken = "bad-token" });

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRefreshToken);
    }

    [Fact]
    public async Task HandleRefreshTokenAsync_MissingSubClaim_ReturnsFail()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([]));
        _jwt.Setup(j => j.ValidateRefreshToken("token-no-sub")).Returns(principal);

        var result = await _sut.HandleRefreshTokenAsync(new RefreshTokenCommand { RefreshToken = "token-no-sub" });

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidTokenClaims);
    }

    [Fact]
    public async Task HandleRefreshTokenAsync_ValidToken_SyncsAndReturnsNewTokens()
    {
        var claims    = new[] { new Claim(JwtRegisteredClaimNames.Sub, "123") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _jwt.Setup(j => j.ValidateRefreshToken("valid-token")).Returns(principal);
        _discordSync.Setup(s => s.SyncUserAndGuildsAsync("123", default)).ReturnsAsync(FakeUser);

        var result = await _sut.HandleRefreshTokenAsync(new RefreshTokenCommand { RefreshToken = "valid-token" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be(FakeAccessToken);
        result.Value.RefreshToken.Should().Be(FakeRefreshToken);
    }

    [Fact]
    public async Task HandleRefreshTokenAsync_SyncThrows_ReturnsFail()
    {
        var claims    = new[] { new Claim(JwtRegisteredClaimNames.Sub, "123") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _jwt.Setup(j => j.ValidateRefreshToken("valid-token")).Returns(principal);
        _discordSync.Setup(s => s.SyncUserAndGuildsAsync("123", default))
            .ThrowsAsync(new InvalidOperationException("User not found."));

        var result = await _sut.HandleRefreshTokenAsync(new RefreshTokenCommand { RefreshToken = "valid-token" });

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be("User not found.");
    }
}
