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

public class UpdateGuildSettingsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService>          _access       = new();
    private readonly Mock<IGuildsRepository>            _guilds       = new();
    private readonly Mock<IDiscordBotService>           _discordBot   = new();
    private readonly Mock<IGuildService>                _guildService = new();
    private readonly Mock<IAuditLogService>             _auditLog     = new();
    private readonly UpdateGuildSettingsCommandHandler  _sut;

    private const string GuildId     = "guild-1";
    private const string RequesterId = "user-1";

    private static readonly UpdateGuildSettingsCommand Command = new()
    {
        GuildId             = GuildId,
        RequesterDiscordId  = RequesterId,
        Timezone            = "Europe/Paris",
        RosterMode          = RosterMode.Open,
        MinRosterRoleId     = null,
    };

    public UpdateGuildSettingsCommandHandlerTests()
    {
        _discordBot.Setup(b => b.Guilds).Returns(_guildService.Object);
        _sut = new UpdateGuildSettingsCommandHandler(_access.Object, _guilds.Object, _discordBot.Object, _auditLog.Object, NullLogger<UpdateGuildSettingsCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotMember_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.None);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RequesterNotAdmin_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = false });

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    [Fact]
    public async Task HandleAsync_Success_ReturnsOkAndCallsUpdateSettings()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true });
        _guilds.Setup(g => g.UpdateSettingsAsync(GuildId, Command.Timezone, Command.RosterMode, null, default))
            .ReturnsAsync(true);

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        _guilds.Verify(g => g.UpdateSettingsAsync(GuildId, Command.Timezone, Command.RosterMode, null, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_FirstTimeConfiguration_OmitsOldValues()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild { Id = GuildId, Name = "Test", IsRegistered = true, Timezone = null, RosterMode = null, MinRosterRoleId = null });

        await _sut.HandleAsync(Command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["newTimezone"] == "Europe/Paris" && v["newRosterMode"] == "Open" &&
                !v.ContainsKey("oldTimezone") && !v.ContainsKey("oldRosterMode") &&
                !v.ContainsKey("oldMinRosterRoleId") && !v.ContainsKey("newMinRosterRoleId")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_ExistingSettings_LogsOldAndNewValues()
    {
        const string oldRoleId = "111111111";
        const string newRoleId = "222222222";
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = newRoleId,
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "UTC", RosterMode = RosterMode.Open, MinRosterRoleId = oldRoleId,
            });
        SetupRoles(
            NetCordTestHelpers.MakeJsonRole(ulong.Parse(oldRoleId), (Permissions)0, name: "Officiers", primaryColor: 0xFF0000),
            NetCordTestHelpers.MakeJsonRole(ulong.Parse(newRoleId), (Permissions)0, name: "Raiders"));

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["oldTimezone"] == "UTC" && v["newTimezone"] == "Europe/Paris" &&
                v["oldRosterMode"] == "Open" && v["newRosterMode"] == "DiscordRoleOnly" &&
                v["oldMinRosterRoleName"] == "Officiers" && v["oldMinRosterRoleColor"] == 0xFF0000.ToString() &&
                v["newMinRosterRoleName"] == "Raiders" && !v.ContainsKey("newMinRosterRoleColor") &&
                !v.ContainsKey("oldMinRosterRoleId") && !v.ContainsKey("newMinRosterRoleId")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_RoleHasIconHash_LogsFullCdnIconUrl()
    {
        const string newRoleId = "222222222";
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = newRoleId,
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = null,
            });
        SetupRoles(NetCordTestHelpers.MakeJsonRole(ulong.Parse(newRoleId), (Permissions)0, name: "Raiders", iconHash: "abc123"));

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["newMinRosterRoleIconUrl"] == $"https://cdn.discordapp.com/role-icons/{newRoleId}/abc123.webp?size=32"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_RoleResolutionFails_StillSucceedsWithoutRoleName()
    {
        const string newRoleId = "222222222";
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = newRoleId,
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = null,
            });
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Throws<InvalidOperationException>();

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["changedFields"] == "minRosterRoleId" && !v.ContainsKey("newMinRosterRoleName")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_OnlyTimezoneChanged_LogsOnlyTimezone()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/London", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
            });

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["oldTimezone"] == "Europe/Paris" && v["newTimezone"] == "Europe/London" &&
                !v.ContainsKey("oldRosterMode") && !v.ContainsKey("newRosterMode") &&
                !v.ContainsKey("oldMinRosterRoleId") && !v.ContainsKey("newMinRosterRoleId")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_OnlyTimezoneChanged_SetsChangedFieldsToTimezoneOnly()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/London", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
            });

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v => v["changedFields"] == "timezone"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_AllThreeChanged_SetsChangedFieldsToAllThree()
    {
        const string oldRoleId = "111111111";
        const string newRoleId = "222222222";
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = newRoleId,
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "UTC", RosterMode = RosterMode.Open, MinRosterRoleId = oldRoleId,
            });
        SetupRoles(
            NetCordTestHelpers.MakeJsonRole(ulong.Parse(oldRoleId), (Permissions)0, name: "Officiers"),
            NetCordTestHelpers.MakeJsonRole(ulong.Parse(newRoleId), (Permissions)0, name: "Raiders"));

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v => v["changedFields"] == "timezone,rosterMode,minRosterRoleId"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_SwitchingToOpenMode_DoesNotLogMinRosterRoleId()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", RosterMode = RosterMode.Open, MinRosterRoleId = null,
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
            });

        await _sut.HandleAsync(command);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.SettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["changedFields"] == "rosterMode" &&
                !v.ContainsKey("oldMinRosterRoleId") && !v.ContainsKey("newMinRosterRoleId")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_NothingChanged_DoesNotLog()
    {
        var command = new UpdateGuildSettingsCommand
        {
            GuildId = GuildId, RequesterDiscordId = RequesterId,
            Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
        };
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default))
            .ReturnsAsync(new Guild
            {
                Id = GuildId, Name = "Test", IsRegistered = true,
                Timezone = "Europe/Paris", RosterMode = RosterMode.DiscordRoleOnly, MinRosterRoleId = "role-1",
            });

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(),
            It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupRoles(params NetCord.JsonModels.JsonRole[] jsonRoles)
    {
        var netcordGuild = NetCordTestHelpers.MakeGuild(1UL, 1UL, new Dictionary<ulong, GuildUser>(), jsonRoles);
        _guildService.Setup(g => g.GetRoles(GuildId, default)).Returns(netcordGuild.Roles.Values);
    }
}
