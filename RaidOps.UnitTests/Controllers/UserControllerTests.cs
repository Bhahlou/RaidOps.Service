using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Authentication.Commands;
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

    [Fact]
    public async Task MarkChangelogSeen_SubClaimMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.MarkChangelogSeen(new MarkChangelogSeenCommand { EntryIds = ["e1"] }, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task MarkChangelogSeen_CommandSucceeds_ReturnsOk()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeContext("user-1");
        _commands.Setup(c => c.DispatchAsync(It.IsAny<MarkChangelogSeenCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("Changelog entries acknowledged.")));

        var result = await _sut.MarkChangelogSeen(new MarkChangelogSeenCommand { EntryIds = ["e1"] }, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(
            It.Is<MarkChangelogSeenCommand>(x => x.RequesterDiscordId == "user-1" && x.EntryIds.SequenceEqual(new[] { "e1" })),
            default), Times.Once);
    }

    [Fact]
    public async Task MarkChangelogSeen_CommandFails_ReturnsBadRequest()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeContext("user-1");
        _commands.Setup(c => c.DispatchAsync(It.IsAny<MarkChangelogSeenCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.UserNotFound));

        var result = await _sut.MarkChangelogSeen(new MarkChangelogSeenCommand { EntryIds = ["e1"] }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
