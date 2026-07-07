using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetCord;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.Application.Guilds.Settings.CommandHandlers;

public class UpdateOfficerThresholdCommandHandlerTests
{
    private readonly Mock<IGuildAccessService>          _access       = new();
    private readonly Mock<IGuildsRepository>            _guilds       = new();
    private readonly Mock<IDiscordBotService>           _discordBot   = new();
    private readonly Mock<IGuildService>                _guildService = new();
    private readonly Mock<IAuditLogService>             _auditLog     = new();
    private readonly UpdateOfficerThresholdCommandHandler _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";
    private const string RoleId      = "111111111";

    public UpdateOfficerThresholdCommandHandlerTests()
    {
        _discordBot.Setup(b => b.Guilds).Returns(_guildService.Object);
        _sut = new UpdateOfficerThresholdCommandHandler(_access.Object, _guilds.Object, _discordBot.Object, _auditLog.Object, NullLogger<UpdateOfficerThresholdCommandHandler>.Instance);
    }

    private static UpdateOfficerThresholdCommand MakeCommand(string roleId = RoleId) => new()
    {
        GuildId             = GuildId,
        RequesterDiscordId  = RequesterId,
        MinOfficerRoleId    = roleId,
    };

    private void SetupRoles(params NetCord.JsonModels.JsonRole[] jsonRoles)
    {
        var netcordGuild = NetCordTestHelpers.MakeGuild(1UL, 1UL, new Dictionary<ulong, GuildUser>(), jsonRoles);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    [Fact]
    public async Task HandleAsync_Success_CallsUpdateOfficerThresholdAsync()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, MinOfficerRoleId = null });
        SetupRoles(NetCordTestHelpers.MakeJsonRole(ulong.Parse(RoleId), (Permissions)0, name: "Officiers"));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guilds.Verify(g => g.UpdateOfficerThresholdAsync(GuildId, RoleId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_FirstTimeConfiguration_LogsWithoutOldRoleName()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, MinOfficerRoleId = null });
        SetupRoles(NetCordTestHelpers.MakeJsonRole(ulong.Parse(RoleId), (Permissions)0, name: "Officiers", primaryColor: 0xFF0000));

        await _sut.HandleAsync(MakeCommand());

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.OfficerThresholdUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["changedFields"] == "minOfficerRoleId" &&
                v["newMinOfficerRoleName"] == "Officiers" &&
                v["newMinOfficerRoleColor"] == 0xFF0000.ToString() &&
                !v.ContainsKey("oldMinOfficerRoleName")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_RoleChanged_LogsOldAndNewRoleNames()
    {
        const string oldRoleId = "222222222";
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, MinOfficerRoleId = oldRoleId });
        SetupRoles(
            NetCordTestHelpers.MakeJsonRole(ulong.Parse(oldRoleId), (Permissions)0, name: "Anciens"),
            NetCordTestHelpers.MakeJsonRole(ulong.Parse(RoleId), (Permissions)0, name: "Officiers"));

        await _sut.HandleAsync(MakeCommand());

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.OfficerThresholdUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["oldMinOfficerRoleName"] == "Anciens" && v["newMinOfficerRoleName"] == "Officiers"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_RoleIconHash_LogsFullCdnIconUrl()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, MinOfficerRoleId = null });
        SetupRoles(NetCordTestHelpers.MakeJsonRole(ulong.Parse(RoleId), (Permissions)0, name: "Officiers", iconHash: "abc123"));

        await _sut.HandleAsync(MakeCommand());

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.OfficerThresholdUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["newMinOfficerRoleIconUrl"] == $"https://cdn.discordapp.com/role-icons/{RoleId}/abc123.webp?size=32"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_BotNotPresent_StillSucceedsWithoutRoleName()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, MinOfficerRoleId = null });
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.OfficerThresholdUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["changedFields"] == "minOfficerRoleId" && !v.ContainsKey("newMinOfficerRoleName")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_Unchanged_DoesNotWriteAuditLog()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, MinOfficerRoleId = RoleId });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(),
            It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
