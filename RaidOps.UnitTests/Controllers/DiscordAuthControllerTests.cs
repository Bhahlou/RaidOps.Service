using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.UnitTests.Controllers;

public class DiscordAuthControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly Mock<IConfiguration>     _config;
    private readonly DiscordAuthController    _sut;

    private const string FrontendUrl = "https://app";

    public DiscordAuthControllerTests()
    {
        _config = ControllerTestHelpers.MakeConfig(("FrontendUrl", FrontendUrl));
        _sut    = new DiscordAuthController(_commands.Object, _queries.Object, _config.Object);
        _sut.ControllerContext = ControllerTestHelpers.MakeContext();
    }

    // ── Constructor guard ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_MissingFrontendUrl_Throws()
    {
        var config = ControllerTestHelpers.MakeConfig();
        var act = () => new DiscordAuthController(_commands.Object, _queries.Object, config.Object);
        act.Should().Throw<InvalidOperationException>().WithMessage("*FrontendUrl*");
    }

    // ── Signup ────────────────────────────────────────────────────────────────

    [Fact]
    public void Signup_ReturnsChallengeResult()
    {
        // Url.Action is needed — mock it
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.Action(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()))
            .Returns("https://api/auth/signupCallback");
        _sut.Url = urlHelper.Object;

        _sut.Signup().Should().BeOfType<ChallengeResult>();
    }

    // ── SignupCallback ────────────────────────────────────────────────────────

    [Fact]
    public async Task SignupCallback_AuthFails_ReturnsUnauthorized()
    {
        var (ctx, _) = ControllerTestHelpers.MakeAuthContext(AuthenticateResult.Fail("failed"));
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _sut.SignupCallback(default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SignupCallback_MissingTokens_ReturnsUnauthorized()
    {
        // Auth succeeds but no tokens in properties
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "user-1")],
                "discord"));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), "discord");
        var (ctx, _) = ControllerTestHelpers.MakeAuthContext(AuthenticateResult.Success(ticket));
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _sut.SignupCallback(default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SignupCallback_SuccessButNullBody_ReturnsBadRequest()
    {
        var ticket = ControllerTestHelpers.MakeSuccessTicket("user-1");
        var (ctx, _) = ControllerTestHelpers.MakeAuthContext(AuthenticateResult.Success(ticket));
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Command succeeds but body is not an AuthenticationResponse
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.SignupCallback(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SignupCallback_CommandFails_ReturnsBadRequest()
    {
        var ticket = ControllerTestHelpers.MakeSuccessTicket("user-1");
        var (ctx, _) = ControllerTestHelpers.MakeAuthContext(AuthenticateResult.Success(ticket));
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        _commands.Setup(c => c.DispatchAsync(It.IsAny<SignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail("auth-failed"));

        var result = await _sut.SignupCallback(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SignupCallback_Success_SetsCookiesAndRedirects()
    {
        var ticket = ControllerTestHelpers.MakeSuccessTicket("user-1");
        var (ctx, _) = ControllerTestHelpers.MakeAuthContext(AuthenticateResult.Success(ticket));
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        var authResp = new AuthenticationResponse
        {
            AccessToken            = "access",
            RefreshToken           = "refresh",
            AccessTokenExpiration  = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpiration = DateTime.UtcNow.AddDays(30),
        };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok", authResp)));

        var result = await _sut.SignupCallback(default);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be($"{FrontendUrl}/authcallback");
        ctx.Response.Headers["Set-Cookie"].Should().NotBeEmpty();
    }

    // ── RefreshToken ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_NoCookie_ReturnsUnauthorized()
    {
        // No cookie set → RefreshToken cookie is null
        var result = await _sut.RefreshToken(default);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RefreshToken_CommandFails_ReturnsUnauthorized()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = "refresh_token=old-jwt";
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        _commands.Setup(c => c.DispatchAsync(It.IsAny<RefreshTokenCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail("invalid"));

        var result = await _sut.RefreshToken(default);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RefreshToken_SuccessButNullBody_ReturnsUnauthorized()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = "refresh_token=old-jwt";
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Command succeeds but body is not an AuthenticationResponse
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RefreshTokenCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.RefreshToken(default);
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RefreshToken_Success_SetsCookiesAndReturnsOk()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = "refresh_token=old-jwt";
        _sut.ControllerContext = new ControllerContext { HttpContext = ctx };

        var authResp = new AuthenticationResponse
        {
            AccessToken            = "new-access",
            RefreshToken           = "new-refresh",
            AccessTokenExpiration  = DateTime.UtcNow.AddMinutes(15),
            RefreshTokenExpiration = DateTime.UtcNow.AddDays(30),
        };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RefreshTokenCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok", authResp)));

        var result = await _sut.RefreshToken(default);

        result.Should().BeOfType<OkResult>();
        ctx.Response.Headers["Set-Cookie"].Should().NotBeEmpty();
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public void Logout_Always_ReturnsOk()
    {
        _sut.Logout().Should().BeOfType<OkResult>();
    }
}
