using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;

namespace RaidOps.UnitTests.Controllers;

public class GuildSettingsControllerTests
{
    private readonly Mock<ICommandDispatcher>   _commands = new();
    private readonly Mock<IQueryDispatcher>     _queries  = new();
    private readonly GuildSettingsController    _sut;

    private const string DiscordId = "user-1";
    private const string GuildId   = "guild-1";

    public GuildSettingsControllerTests()
    {
        _sut = new GuildSettingsController(_commands.Object, _queries.Object)
        {
            ControllerContext = ControllerTestHelpers.MakeContext(DiscordId)
        };
    }

    // ── GetSettings ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettings_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetSettings(GuildId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSettings_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
                It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Fail(ResponseDetail.GuildNotFound));

        var result = await _sut.GetSettings(GuildId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetSettings_Success_ReturnsOkWithResponse()
    {
        var response = new GuildSettingsResponse { Timezone = "Europe/Paris" };
        _queries.Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
                It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(response));

        var result = await _sut.GetSettings(GuildId, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetSettings_PassesCorrectQueryFields()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
                It.IsAny<GetGuildSettingsQuery>(), default))
            .ReturnsAsync(Result<GuildSettingsResponse>.Ok(new GuildSettingsResponse()));

        await _sut.GetSettings(GuildId, default);

        _queries.Verify(q => q.DispatchAsync<GetGuildSettingsQuery, GuildSettingsResponse>(
            It.Is<GetGuildSettingsQuery>(x => x.GuildId == GuildId && x.RequesterDiscordId == DiscordId),
            default), Times.Once);
    }

    // ── GetDiscordRoles ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDiscordRoles_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetDiscordRoles(GuildId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetDiscordRoles_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildDiscordRolesQuery, List<DiscordRoleResponse>>(
                It.IsAny<GetGuildDiscordRolesQuery>(), default))
            .ReturnsAsync(Result<List<DiscordRoleResponse>>.Fail(ResponseDetail.GuildBotNotPresent));

        var result = await _sut.GetDiscordRoles(GuildId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetDiscordRoles_Success_ReturnsOkWithRoles()
    {
        var roles = new List<DiscordRoleResponse> { new() { Id = "r1", Name = "Officer" } };
        _queries.Setup(q => q.DispatchAsync<GetGuildDiscordRolesQuery, List<DiscordRoleResponse>>(
                It.IsAny<GetGuildDiscordRolesQuery>(), default))
            .ReturnsAsync(Result<List<DiscordRoleResponse>>.Ok(roles));

        var result = await _sut.GetDiscordRoles(GuildId, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(roles);
    }

    // ── UpdateSettings ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSettings_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new UpdateGuildSettingsCommand { Timezone = "UTC", Language = "en" };

        var result = await _sut.UpdateSettings(GuildId, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateSettings_CommandFails_ReturnsBadRequest()
    {
        var command = new UpdateGuildSettingsCommand { Timezone = "UTC", Language = "en" };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildSettingsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.UpdateSettings(GuildId, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateSettings_Success_SetsRouteFieldsAndReturnsOk()
    {
        var command = new UpdateGuildSettingsCommand { Timezone = "Europe/Paris", Language = "en" };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildSettingsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.UpdateSettings(GuildId, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
    }

    // ── GetNotificationSettings ──────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationSettings_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetNotificationSettings(GuildId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetNotificationSettings_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildNotificationSettingsQuery, List<GuildNotificationSettingResponse>>(
                It.IsAny<GetGuildNotificationSettingsQuery>(), default))
            .ReturnsAsync(Result<List<GuildNotificationSettingResponse>>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.GetNotificationSettings(GuildId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetNotificationSettings_Success_ReturnsOkWithResponse()
    {
        var response = new List<GuildNotificationSettingResponse>();
        _queries.Setup(q => q.DispatchAsync<GetGuildNotificationSettingsQuery, List<GuildNotificationSettingResponse>>(
                It.IsAny<GetGuildNotificationSettingsQuery>(), default))
            .ReturnsAsync(Result<List<GuildNotificationSettingResponse>>.Ok(response));

        var result = await _sut.GetNotificationSettings(GuildId, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetNotificationSettings_PassesCorrectQueryFields()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildNotificationSettingsQuery, List<GuildNotificationSettingResponse>>(
                It.IsAny<GetGuildNotificationSettingsQuery>(), default))
            .ReturnsAsync(Result<List<GuildNotificationSettingResponse>>.Ok([]));

        await _sut.GetNotificationSettings(GuildId, default);

        _queries.Verify(q => q.DispatchAsync<GetGuildNotificationSettingsQuery, List<GuildNotificationSettingResponse>>(
            It.Is<GetGuildNotificationSettingsQuery>(x => x.GuildId == GuildId && x.RequesterDiscordId == DiscordId),
            default), Times.Once);
    }

    // ── GetNotificationChannels ──────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationChannels_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();

        var result = await _sut.GetNotificationChannels(GuildId, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetNotificationChannels_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildNotificationChannelsQuery, List<DiscordChannelResponse>>(
                It.IsAny<GetGuildNotificationChannelsQuery>(), default))
            .ReturnsAsync(Result<List<DiscordChannelResponse>>.Fail(ResponseDetail.GuildBotNotPresent));

        var result = await _sut.GetNotificationChannels(GuildId, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetNotificationChannels_Success_ReturnsOkWithResponse()
    {
        var response = new List<DiscordChannelResponse>();
        _queries.Setup(q => q.DispatchAsync<GetGuildNotificationChannelsQuery, List<DiscordChannelResponse>>(
                It.IsAny<GetGuildNotificationChannelsQuery>(), default))
            .ReturnsAsync(Result<List<DiscordChannelResponse>>.Ok(response));

        var result = await _sut.GetNotificationChannels(GuildId, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    // ── UpdateNotificationSettings ────────────────────────────────────────────

    [Fact]
    public async Task UpdateNotificationSettings_SubMissing_ReturnsUnauthorized()
    {
        _sut.ControllerContext = ControllerTestHelpers.MakeAnonymousContext();
        var command = new UpdateGuildNotificationSettingsCommand { Settings = [] };

        var result = await _sut.UpdateNotificationSettings(GuildId, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateNotificationSettings_CommandFails_ReturnsBadRequest()
    {
        var command = new UpdateGuildNotificationSettingsCommand { Settings = [] };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildNotificationSettingsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.Forbidden));

        var result = await _sut.UpdateNotificationSettings(GuildId, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateNotificationSettings_Success_SetsRouteFieldsAndReturnsOk()
    {
        var command = new UpdateGuildNotificationSettingsCommand { Settings = [] };
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildNotificationSettingsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await _sut.UpdateNotificationSettings(GuildId, command, default);

        result.Should().BeOfType<OkObjectResult>();
        command.GuildId.Should().Be(GuildId);
        command.RequesterDiscordId.Should().Be(DiscordId);
    }
}
