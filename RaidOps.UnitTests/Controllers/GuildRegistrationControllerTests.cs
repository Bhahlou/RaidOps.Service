using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.UnitTests.Controllers;

public class GuildRegistrationControllerTests
{
    private readonly Mock<ICommandDispatcher>  _commands = new();
    private readonly Mock<IQueryDispatcher>    _queries  = new();
    private readonly Mock<IJwtService>         _jwt      = new();
    private readonly Mock<IConfiguration>      _config;
    private readonly GuildRegistrationController _sut;

    private const string DiscordId   = "user-1";
    private const string GuildId     = "guild-1";
    private const string FrontendUrl = "https://app";

    public GuildRegistrationControllerTests()
    {
        _config = ControllerTestHelpers.MakeConfig(
            ("FrontendUrl",            FrontendUrl),
            ("Discord:ClientId",       "discord-client-id"),
            ("Discord:BotPermissions", "8"));

        _sut = new GuildRegistrationController(_commands.Object, _queries.Object, _jwt.Object, _config.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("https://api/guilds/callback");
        _sut.Url = urlHelper.Object;
    }

    // ── Constructor guards ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Discord:ClientId",       "Discord:BotPermissions", "8",  "FrontendUrl")]
    [InlineData("FrontendUrl",            "Discord:ClientId",       "id", "Discord:BotPermissions")]
    [InlineData("Discord:BotPermissions", "FrontendUrl",            "u",  "Discord:ClientId")]
    public void Constructor_MissingConfig_Throws(string key1, string key2, string val2, string missingKey)
    {
        var config = ControllerTestHelpers.MakeConfig((key1, "https://app"), (key2, val2));
        var act = () => new GuildRegistrationController(_commands.Object, _queries.Object, _jwt.Object, config.Object);
        act.Should().Throw<InvalidOperationException>().WithMessage($"*{missingKey}*");
    }

    // ── Initiate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Initiate_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        _sut.Initiate(GuildId).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void Initiate_Success_ReturnsRedirectToDiscord()
    {
        _jwt.Setup(j => j.GenerateStateToken(GuildId, DiscordId, null)).Returns("state-token");

        var result = _sut.Initiate(GuildId);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().StartWith("https://discord.com/oauth2/authorize");
    }

    [Fact]
    public void Initiate_WithAllowedReturnTo_PassesItToStateToken()
    {
        _jwt.Setup(j => j.GenerateStateToken(GuildId, DiscordId, "get-started")).Returns("state-token");

        _sut.Initiate(GuildId, "get-started");

        _jwt.Verify(j => j.GenerateStateToken(GuildId, DiscordId, "get-started"), Times.Once);
    }

    [Fact]
    public void Initiate_WithUnrecognizedReturnTo_IsIgnored()
    {
        _jwt.Setup(j => j.GenerateStateToken(GuildId, DiscordId, null)).Returns("state-token");

        _sut.Initiate(GuildId, "evil-redirect");

        _jwt.Verify(j => j.GenerateStateToken(GuildId, DiscordId, null), Times.Once);
    }

    // ── Callback ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Callback_SubMissing_RedirectsWithUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var result = await _sut.Callback(GuildId, "state", default);
        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Contain("unauthorized");
    }

    [Fact]
    public async Task Callback_NullParams_RedirectsWithInvalidRequest()
    {
        var result = await _sut.Callback(null, null, default);
        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Contain("invalid_request");
    }

    [Fact]
    public async Task Callback_InvalidState_RedirectsWithInvalidState()
    {
        _jwt.Setup(j => j.ValidateStateToken("bad-state"))
            .Returns(((string, string, string?)?)null);

        var result = await _sut.Callback(GuildId, "bad-state", default);
        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Contain("invalid_state");
    }

    [Fact]
    public async Task Callback_StateMismatch_RedirectsWithStateMismatch()
    {
        _jwt.Setup(j => j.ValidateStateToken("state"))
            .Returns((GuildId: "other-guild", DiscordId: "other-user", ReturnTo: (string?)null));

        var result = await _sut.Callback(GuildId, "state", default);
        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Contain("state_mismatch");
    }

    [Fact]
    public async Task Callback_RegisterFails_RedirectsWithRegisterFailed()
    {
        _jwt.Setup(j => j.ValidateStateToken("state"))
            .Returns((GuildId, DiscordId, (string?)null));
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RegisterGuildCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.GuildBotNotPresent));

        var result = await _sut.Callback(GuildId, "state", default);
        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Contain("register_failed");
    }

    [Fact]
    public async Task Callback_Success_RedirectsToGuildRegisterPage()
    {
        _jwt.Setup(j => j.ValidateStateToken("state"))
            .Returns((GuildId, DiscordId, (string?)null));
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RegisterGuildCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("registered")));

        var result = await _sut.Callback(GuildId, "state", default);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be($"{FrontendUrl}/guild-register/{GuildId}");
    }

    [Fact]
    public async Task Callback_Success_WithGetStartedReturnTo_RedirectsToGetStarted()
    {
        _jwt.Setup(j => j.ValidateStateToken("state"))
            .Returns((GuildId, DiscordId, "get-started"));
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RegisterGuildCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("registered")));

        var result = await _sut.Callback(GuildId, "state", default);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be($"{FrontendUrl}/get-started");
    }

    [Fact]
    public async Task Callback_CancelledWithGetStartedReturnTo_RedirectsToGetStartedWithError()
    {
        // Discord omits guild_id when the user cancels the bot-invite consent screen, but still
        // echoes back state — the cancelled flow should still return to get-started, not strand
        // the user on /no-guild.
        _jwt.Setup(j => j.ValidateStateToken("state"))
            .Returns((GuildId, DiscordId, "get-started"));

        var result = await _sut.Callback(null, "state", default);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be($"{FrontendUrl}/get-started?error=invalid_request");
    }

    [Fact]
    public async Task Callback_CancelledWithoutReturnTo_RedirectsToNoGuild()
    {
        _jwt.Setup(j => j.ValidateStateToken("state"))
            .Returns((GuildId, DiscordId, (string?)null));

        var result = await _sut.Callback(null, "state", default);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be($"{FrontendUrl}/no-guild?error=invalid_request");
    }
}
