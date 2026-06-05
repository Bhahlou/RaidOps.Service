using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Authentication.Queries;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.UnitTests.Controllers;

public class UserControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly UserController           _sut;

    public UserControllerTests()
    {
        _sut = new UserController(_commands.Object, _queries.Object);
    }

    [Fact]
    public async Task GetMe_SubClaimMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetMe(default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMe_QuerySucceeds_ReturnsOkWithUser()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeContext("user-1");
        var user = new UserResponse { DiscordId = "user-1", Name = "Bhahlou", Guilds = [] };
        _queries.Setup(q => q.DispatchAsync<GetMeQuery, UserResponse>(
                It.Is<GetMeQuery>(x => x.DiscordId == "user-1"), default))
            .ReturnsAsync(Result<UserResponse>.Ok(user));

        var result = await _sut.GetMe(default);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(user);
    }

    [Fact]
    public async Task GetMe_QueryFails_ReturnsBadRequest()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeContext("user-1");
        _queries.Setup(q => q.DispatchAsync<GetMeQuery, UserResponse>(It.IsAny<GetMeQuery>(), default))
            .ReturnsAsync(Result<UserResponse>.Fail(ResponseDetail.UserNotFound));

        var result = await _sut.GetMe(default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
