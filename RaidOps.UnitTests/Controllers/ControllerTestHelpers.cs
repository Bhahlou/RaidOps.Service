using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RaidOps.UnitTests.Controllers;

internal static class ControllerTestHelpers
{
    // ── HttpContext / User ────────────────────────────────────────────────────

    internal static ControllerContext MakeContext(string? discordId = "user-1")
    {
        var ctx = new DefaultHttpContext();
        if (discordId is not null)
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtRegisteredClaimNames.Sub, discordId)], "jwt"));
        return new ControllerContext { HttpContext = ctx };
    }

    /// <summary>Context with no sub claim — simulates unauthenticated / missing claim.</summary>
    internal static ControllerContext MakeAnonymousContext()
    {
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal() };
        return new ControllerContext { HttpContext = ctx };
    }

    // ── IAuthenticationService ───────────────────────────────────────────────

    internal static (DefaultHttpContext ctx, Mock<IAuthenticationService> authMock)
        MakeAuthContext(AuthenticateResult authResult)
    {
        var authMock = new Mock<IAuthenticationService>();
        authMock.Setup(s => s.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(authResult);
        authMock.Setup(s => s.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()))
            .Returns(Task.CompletedTask);

        var sp = new ServiceCollection()
            .AddSingleton(authMock.Object)
            .BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = sp };
        return (ctx, authMock);
    }

    internal static AuthenticationTicket MakeSuccessTicket(
        string discordId      = "user-1",
        string accessToken    = "discord-access",
        string refreshToken   = "discord-refresh")
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, discordId)], "discord"));

        return new AuthenticationTicket(principal,
            new AuthenticationProperties(new Dictionary<string, string?> {
                [".Token.access_token"]  = accessToken,
                [".Token.refresh_token"] = refreshToken,
            }),
            "discord");
    }

    // ── IConfiguration mock ───────────────────────────────────────────────────

    internal static Mock<IConfiguration> MakeConfig(params (string key, string value)[] entries)
    {
        var config = new Mock<IConfiguration>();
        foreach (var (key, value) in entries)
            config.Setup(c => c[key]).Returns(value);
        return config;
    }
}
