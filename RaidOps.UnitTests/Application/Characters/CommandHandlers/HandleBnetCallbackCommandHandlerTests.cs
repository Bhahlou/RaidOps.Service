using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.CommandHandlers;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Characters.CommandHandlers;

public class HandleBnetCallbackCommandHandlerTests
{
    private readonly Mock<IJwtService>             _jwt          = new();
    private readonly Mock<IBnetApiService>         _bnetApi      = new();
    private readonly Mock<IBnetAccountRepository>  _bnetAccounts = new();
    private readonly HandleBnetCallbackCommandHandler _sut;

    private const string DiscordId   = "user-1";
    private const string Code        = "auth-code";
    private const string State       = "state-token";
    private const string CallbackUrl = "https://app/bnet/callback";
    private const string Region      = "eu";

    private static readonly HandleBnetCallbackCommand Command = new()
    {
        DiscordId   = DiscordId,
        Code        = Code,
        State       = State,
        CallbackUrl = CallbackUrl,
    };

    private static readonly BnetTokenResponse TokenResponse = new()
    {
        AccessToken  = "bnet-access",
        RefreshToken = "bnet-refresh",
        ExpiresIn    = 86400,
    };

    private static readonly BnetUserInfoResponse UserInfo = new()
    {
        Id        = 42,
        BattleTag = "Player#1234",
    };

    public HandleBnetCallbackCommandHandlerTests()
    {
        _sut = new HandleBnetCallbackCommandHandler(_jwt.Object, _bnetApi.Object, _bnetAccounts.Object);
    }

    // ── Guard clauses ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_InvalidStateToken_ReturnsInvalidState()
    {
        _jwt.Setup(j => j.ValidateBnetStateToken(State))
            .Returns((DiscordId: (string)null!, Region: (string)null!)!);

        // Moq returns the default for a nullable value type (null for Nullable<T>)
        _jwt.Setup(j => j.ValidateBnetStateToken(State))
            .Returns((ValueTuple<string, string>?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidState);
    }

    [Fact]
    public async Task HandleAsync_DiscordIdMismatch_ReturnsStateMismatch()
    {
        _jwt.Setup(j => j.ValidateBnetStateToken(State))
            .Returns((DiscordId: "other-user", Region));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.StateMismatch);
    }

    // ── BNet API error ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_BnetApiThrows_ReturnsBnetApiError()
    {
        _jwt.Setup(j => j.ValidateBnetStateToken(State))
            .Returns((DiscordId, Region));

        _bnetApi.Setup(b => b.ExchangeCodeAsync(Code, CallbackUrl, Region, default))
            .ThrowsAsync(new HttpRequestException("BNet unreachable"));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.BnetApiError);
        _bnetAccounts.Verify(r => r.UpsertAsync(It.IsAny<BattleNetAccount>(), default), Times.Never);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_UpsertsAccountAndReturnsOk()
    {
        _jwt.Setup(j => j.ValidateBnetStateToken(State))
            .Returns((DiscordId, Region));
        _bnetApi.Setup(b => b.ExchangeCodeAsync(Code, CallbackUrl, Region, default))
            .ReturnsAsync(TokenResponse);
        _bnetApi.Setup(b => b.GetUserInfoAsync(TokenResponse.AccessToken, Region, default))
            .ReturnsAsync(UserInfo);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _bnetAccounts.Verify(r => r.UpsertAsync(
            It.Is<BattleNetAccount>(a =>
                a.UserDiscordId == DiscordId &&
                a.BnetId        == "42"      &&
                a.BattleTag     == "Player#1234" &&
                a.AccessToken   == "bnet-access" &&
                a.Region        == Region),
            default), Times.Once);
    }
}
