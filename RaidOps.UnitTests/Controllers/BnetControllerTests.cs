using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.UnitTests.Controllers;

public class BnetControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly Mock<IConfiguration>     _config;
    private readonly BnetController           _sut;

    private const string DiscordId   = "user-1";
    private const string FrontendUrl = "https://app";
    private const string CallbackUrl = "https://api/bnet/callback";

    public BnetControllerTests()
    {
        _config = ControllerTestHelpers.MakeConfig(
            ("FrontendUrl",          FrontendUrl),
            ("BattleNet:CallbackUrl", CallbackUrl));

        _sut = new BnetController(_commands.Object, _queries.Object, _config.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_MissingFrontendUrl_Throws()
    {
        var config = ControllerTestHelpers.MakeConfig(("BattleNet:CallbackUrl", CallbackUrl));
        var act = () => new BnetController(_commands.Object, _queries.Object, config.Object);
        act.Should().Throw<InvalidOperationException>().WithMessage("*FrontendUrl*");
    }

    [Fact]
    public void Constructor_MissingCallbackUrl_Throws()
    {
        var config = ControllerTestHelpers.MakeConfig(("FrontendUrl", FrontendUrl));
        var act = () => new BnetController(_commands.Object, _queries.Object, config.Object);
        act.Should().Throw<InvalidOperationException>().WithMessage("*CallbackUrl*");
    }

    // ── GetAccounts ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccounts_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.GetAccounts(default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetAccounts_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetBnetAccountsQuery, List<BnetAccountResponse>>(
                It.IsAny<GetBnetAccountsQuery>(), default))
            .ReturnsAsync(Result<List<BnetAccountResponse>>.Fail("some-error"));

        (await _sut.GetAccounts(default)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAccounts_NoneLinked_ReturnsOkWithEmptyList()
    {
        _queries.Setup(q => q.DispatchAsync<GetBnetAccountsQuery, List<BnetAccountResponse>>(
                It.IsAny<GetBnetAccountsQuery>(), default))
            .ReturnsAsync(Result<List<BnetAccountResponse>>.Ok([]));

        var result = await _sut.GetAccounts(default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new List<BnetAccountResponse>());
    }

    [Fact]
    public async Task GetAccounts_QuerySucceeds_ReturnsOkWithAccounts()
    {
        var accounts = new List<BnetAccountResponse>
        {
            new() { BnetId = "42", BattleTag = "Player#1234", Region = "eu", TokenExpiry = DateTimeOffset.UtcNow.AddHours(1) },
        };
        _queries.Setup(q => q.DispatchAsync<GetBnetAccountsQuery, List<BnetAccountResponse>>(
                It.IsAny<GetBnetAccountsQuery>(), default))
            .ReturnsAsync(Result<List<BnetAccountResponse>>.Ok(accounts));

        var result = await _sut.GetAccounts(default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(accounts);
    }

    // ── Unlink ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.Unlink("bnet-1", default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Unlink_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UnlinkBnetAccountCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail("some-error"));

        (await _sut.Unlink("bnet-1", default)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Unlink_CommandSucceeds_ReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(
                It.Is<UnlinkBnetAccountCommand>(cmd => cmd.UserDiscordId == DiscordId && cmd.BnetId == "bnet-1"), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("unlinked")));

        var result = await _sut.Unlink("bnet-1", default);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── Initiate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Initiate_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        (await _sut.Initiate("eu")).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Initiate_InvalidRegion_ReturnsBadRequest()
    {
        (await _sut.Initiate("invalid")).Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Initiate_ValidRegion_ReturnsRedirect()
    {
        _queries.Setup(q => q.DispatchAsync<GetBnetAuthorizationUrlQuery, string>(
                It.IsAny<GetBnetAuthorizationUrlQuery>(), default))
            .ReturnsAsync(Result<string>.Ok("https://oauth.battle.net/authorize?..."));

        var result = await _sut.Initiate("eu");

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().StartWith("https://oauth.battle.net");
    }

    // ── Callback ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Callback_SubMissing_RedirectsWithError()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var result = await _sut.Callback("code", "state", default);
        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Contain("error=");
    }

    [Fact]
    public async Task Callback_MissingParams_RedirectsWithError()
    {
        var result = await _sut.Callback(null, null, default);
        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Contain("error=");
    }

    [Fact]
    public async Task Callback_CommandFails_RedirectsWithError()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<HandleBnetCallbackCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.BnetApiError));

        var result = await _sut.Callback("code", "state", default);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Contain("error=");
    }

    [Fact]
    public async Task Callback_CommandSucceeds_RedirectsToSuccess()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<HandleBnetCallbackCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("linked")));

        var result = await _sut.Callback("code", "state", default);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Contain("bnet_linked=true");
    }
}
