using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Dev.Commands;

namespace RaidOps.UnitTests.Controllers;

public class DevControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();
    private readonly Mock<IHostEnvironment> _environment = new();
    private readonly DevController _sut;

    private const string DiscordId = "user-1";
    private const string GuildId = "guild-1";

    public DevControllerTests()
    {
        _sut = new DevController(_commands.Object, _queries.Object, _environment.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    [Fact]
    public async Task ResetOnboarding_NotDevelopmentEnvironment_ReturnsNotFound()
    {
        _environment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var result = await _sut.ResetOnboarding(GuildId, default);

        result.Should().BeOfType<NotFoundResult>();
        _commands.Verify(c => c.DispatchAsync(It.IsAny<ResetGuildOnboardingCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task ResetOnboarding_SubMissing_ReturnsUnauthorized()
    {
        _environment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.ResetOnboarding(GuildId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ResetOnboarding_CommandFails_ReturnsBadRequest()
    {
        _environment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ResetGuildOnboardingCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest));

        var result = await _sut.ResetOnboarding(GuildId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ResetOnboarding_Success_PassesCorrectFieldsAndReturnsOk()
    {
        _environment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ResetGuildOnboardingCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.ResetOnboarding(GuildId, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(
            It.Is<ResetGuildOnboardingCommand>(x => x.GuildId == GuildId && x.UserDiscordId == DiscordId),
            default), Times.Once);
    }
}
