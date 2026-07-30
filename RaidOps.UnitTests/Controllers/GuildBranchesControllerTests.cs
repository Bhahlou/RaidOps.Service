using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Guilds.Branches.Queries;
using RaidOps.Application.Contracts.Guilds.Branches.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Controllers;

public class GuildBranchesControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher>   _queries  = new();
    private readonly GuildBranchesController  _sut;

    private const string DiscordId     = "user-1";
    private const string GuildId       = "guild-1";
    private const int    GuildBranchId = 1;

    public GuildBranchesControllerTests()
    {
        _sut = new GuildBranchesController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── GetBranches ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBranches_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetBranches(GuildId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetBranches_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildBranchesQuery, List<GuildBranchResponse>>(
                It.IsAny<GetGuildBranchesQuery>(), default))
            .ReturnsAsync(Result<List<GuildBranchResponse>>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.GetBranches(GuildId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBranches_Success_ReturnsOkWithResponse()
    {
        var response = new List<GuildBranchResponse> { new() { Id = 1, BranchId = 2, BranchName = "Classic Era" } };
        _queries.Setup(q => q.DispatchAsync<GetGuildBranchesQuery, List<GuildBranchResponse>>(
                It.IsAny<GetGuildBranchesQuery>(), default))
            .ReturnsAsync(Result<List<GuildBranchResponse>>.Ok(response));

        var result = await _sut.GetBranches(GuildId, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetBranches_PassesCorrectQueryFields()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildBranchesQuery, List<GuildBranchResponse>>(
                It.IsAny<GetGuildBranchesQuery>(), default))
            .ReturnsAsync(Result<List<GuildBranchResponse>>.Ok([]));

        await _sut.GetBranches(GuildId, default);

        _queries.Verify(q => q.DispatchAsync<GetGuildBranchesQuery, List<GuildBranchResponse>>(
            It.Is<GetGuildBranchesQuery>(x => x.GuildId == GuildId && x.RequesterDiscordId == DiscordId),
            default), Times.Once);
    }

    // ── ActivateBranch ─────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateBranch_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new ActivateGuildBranchCommand { BranchId = 2 };

        var result = await _sut.ActivateBranch(GuildId, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ActivateBranch_CommandFails_ReturnsBadRequest()
    {
        var command = new ActivateGuildBranchCommand { BranchId = 2 };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ActivateGuildBranchCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.ActivateBranch(GuildId, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ActivateBranch_Success_SetsRouteFieldsAndReturnsOk()
    {
        var command = new ActivateGuildBranchCommand { BranchId = 2 };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ActivateGuildBranchCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.ActivateBranch(GuildId, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
    }

    // ── DeactivateBranch ───────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateBranch_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.DeactivateBranch(GuildId, GuildBranchId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeactivateBranch_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeactivateGuildBranchCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.GuildBranchNotFound));

        var result = await _sut.DeactivateBranch(GuildId, GuildBranchId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeactivateBranch_Success_PassesCorrectFieldsAndReturnsOk()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeactivateGuildBranchCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.DeactivateBranch(GuildId, GuildBranchId, default);

        result.Should().BeOfType<OkObjectResult>();
        _commands.Verify(c => c.DispatchAsync(
            It.Is<DeactivateGuildBranchCommand>(x => x.GuildId == GuildId && x.GuildBranchId == GuildBranchId && x.RequesterDiscordId == DiscordId),
            default), Times.Once);
    }

    // ── UpdateRosterSettings ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateRosterSettings_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new UpdateGuildBranchRosterSettingsCommand { RosterMode = RosterMode.Open };

        var result = await _sut.UpdateRosterSettings(GuildId, GuildBranchId, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateRosterSettings_CommandFails_ReturnsBadRequest()
    {
        var command = new UpdateGuildBranchRosterSettingsCommand { RosterMode = RosterMode.Open };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildBranchRosterSettingsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.UpdateRosterSettings(GuildId, GuildBranchId, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateRosterSettings_Success_SetsRouteFieldsAndReturnsOk()
    {
        var command = new UpdateGuildBranchRosterSettingsCommand { RosterMode = RosterMode.Open };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildBranchRosterSettingsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.UpdateRosterSettings(GuildId, GuildBranchId, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.GuildBranchId.Should().Be(GuildBranchId);
        command.RequesterDiscordId.Should().Be(DiscordId);
    }
}
