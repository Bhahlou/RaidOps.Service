using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.CommandHandlers;

public class ResetGuildNotificationSettingsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettings = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly ResetGuildNotificationSettingsCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int GuildBranchId = 7;

    public ResetGuildNotificationSettingsCommandHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new ResetGuildNotificationSettingsCommandHandler(
            _access.Object, _notificationSettings.Object, _discordBotService.Object, _auditLog.Object);
    }

    private static ResetGuildNotificationSettingsCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventType = GuildNotificationEventType.AbsenceAdded,
    };

    [Fact]
    public async Task HandleAsync_RequesterNotBranchOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _notificationSettings.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<GuildNotificationEventType>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoOverrideExisted_DeletesButDoesNotLogAudit()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _notificationSettings.Setup(r => r.GetAllForGuildAsync(GuildId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _notificationSettings.Verify(r => r.DeleteAsync(GuildId, GuildBranchId, GuildNotificationEventType.AbsenceAdded, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_OverrideExisted_DeletesAndLogsOldOverrideAndNewEffectiveDisabled()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _notificationSettings.Setup(r => r.GetAllForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = GuildId, GuildBranchId = GuildBranchId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
        ]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns(
        [
            new DiscordChannelInfo(111, "branch-log", [], null),
        ]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _notificationSettings.Verify(r => r.DeleteAsync(GuildId, GuildBranchId, GuildNotificationEventType.AbsenceAdded, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsReset,
            It.Is<Dictionary<string, string>>(v =>
                v["guildBranchId"] == GuildBranchId.ToString() &&
                v["eventType"] == "AbsenceAdded" &&
                v["changedEvents"] == "AbsenceAdded" &&
                v["oldAbsenceAddedEnabled"] == "true" &&
                v["oldAbsenceAddedChannelName"] == "branch-log" &&
                v["newAbsenceAddedEnabled"] == "false" &&
                !v.ContainsKey("newAbsenceAddedChannelName")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OverrideExisted_FallsBackToGuildWideChannel()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _notificationSettings.Setup(r => r.GetAllForGuildAsync(GuildId, default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = GuildId, GuildBranchId = GuildBranchId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
            new GuildNotificationSetting { GuildId = GuildId, GuildBranchId = null, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "222" },
        ]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns(
        [
            new DiscordChannelInfo(111, "branch-log", [], null),
            new DiscordChannelInfo(222, "guild-log", [], null),
        ]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsReset,
            It.Is<Dictionary<string, string>>(v =>
                v["oldAbsenceAddedChannelName"] == "branch-log" &&
                v["newAbsenceAddedEnabled"] == "true" &&
                v["newAbsenceAddedChannelName"] == "guild-log"),
            default), Times.Once);
    }
}
