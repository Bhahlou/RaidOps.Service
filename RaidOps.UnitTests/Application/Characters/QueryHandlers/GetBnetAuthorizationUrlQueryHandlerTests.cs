using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Characters.QueryHandlers;
using RaidOps.ExternalApplication.Contracts.Services.BNet;

namespace RaidOps.UnitTests.Application.Characters.QueryHandlers;

public class GetBnetAuthorizationUrlQueryHandlerTests
{
    private readonly Mock<IJwtService>     _jwt     = new();
    private readonly Mock<IBnetApiService> _bnetApi = new();
    private readonly GetBnetAuthorizationUrlQueryHandler _sut;

    private const string DiscordId   = "user-1";
    private const string Region      = "eu";
    private const string CallbackUrl = "https://app/callback";
    private const string StateToken  = "signed-state-token";
    private const string ExpectedUrl = "https://oauth.battle.net/authorize?...";

    private static readonly GetBnetAuthorizationUrlQuery Query = new()
    {
        DiscordId   = DiscordId,
        Region      = Region,
        CallbackUrl = CallbackUrl,
    };

    public GetBnetAuthorizationUrlQueryHandlerTests()
    {
        _sut = new GetBnetAuthorizationUrlQueryHandler(_jwt.Object, _bnetApi.Object);
    }

    [Fact]
    public async Task HandleAsync_ReturnsUrlBuiltByBnetService()
    {
        _jwt.Setup(j => j.GenerateBnetStateToken(DiscordId, Region)).Returns(StateToken);
        _bnetApi.Setup(b => b.BuildAuthorizationUrl(Region, CallbackUrl, StateToken)).Returns(ExpectedUrl);

        var result = await _sut.HandleAsync(Query, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ExpectedUrl);
    }

    [Fact]
    public async Task HandleAsync_PassesStateTokenFromJwtToBnetService()
    {
        _jwt.Setup(j => j.GenerateBnetStateToken(DiscordId, Region)).Returns(StateToken);
        _bnetApi.Setup(b => b.BuildAuthorizationUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(ExpectedUrl);

        await _sut.HandleAsync(Query, default);

        _bnetApi.Verify(b => b.BuildAuthorizationUrl(Region, CallbackUrl, StateToken), Times.Once);
    }
}
