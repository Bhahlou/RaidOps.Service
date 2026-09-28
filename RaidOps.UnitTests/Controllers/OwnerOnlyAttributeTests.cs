using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RaidOps.API.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RaidOps.UnitTests.Controllers;

/// <summary>
/// Unit tests for <see cref="OwnerOnlyAttribute"/> — the owner-only gate backing every
/// <see cref="RaidOps.API.Controllers.v1.AdminController"/> action.
/// </summary>
public class OwnerOnlyAttributeTests
{
    private const string OwnerId = "111111111111111111";

    private static AuthorizationFilterContext MakeContext(string? discordId, string? ownerIds)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Admin:OwnerDiscordIds"]).Returns(ownerIds);

        var services = new ServiceCollection().AddSingleton(config.Object).BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.User = discordId is null
            ? new ClaimsPrincipal()
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, discordId)], "jwt"));

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Fact]
    public void OnAuthorization_SubClaimMissing_SetsUnauthorizedResult()
    {
        var context = MakeContext(discordId: null, ownerIds: OwnerId);

        new OwnerOnlyAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void OnAuthorization_SubNotInOwnerList_SetsForbidResult()
    {
        var context = MakeContext(OwnerId, "222222222222222222,333333333333333333");

        new OwnerOnlyAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" , ,")]
    public void OnAuthorization_OwnerListMissingOrEmpty_SetsForbidResult(string? ownerIds)
    {
        var context = MakeContext(OwnerId, ownerIds);

        new OwnerOnlyAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void OnAuthorization_OwnerListIsAPrefixOfTheSub_SetsForbidResult()
    {
        // Entries must match exactly — a shorter/longer ID that merely overlaps is not the owner.
        var context = MakeContext(OwnerId, "11111111111111111");

        new OwnerOnlyAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(OwnerId)]
    [InlineData("222222222222222222, 111111111111111111 ,333333333333333333")]
    [InlineData("  111111111111111111  ")]
    [InlineData("222222222222222222,,111111111111111111")]
    public void OnAuthorization_SubInCommaSeparatedList_LeavesResultUnset(string ownerIds)
    {
        var context = MakeContext(OwnerId, ownerIds);

        new OwnerOnlyAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }
}
