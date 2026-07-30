using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.CommandHandlers;

public class ResetGuildNotificationSettingsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettings = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly ResetGuildNotificationSettingsCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";
    private const int GuildBranchId = 7;

    public ResetGuildNotificationSettingsCommandHandlerTests()
    {
        _sut = new ResetGuildNotificationSettingsCommandHandler(_access.Object, _notificationSettings.Object, _auditLog.Object);
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
    public async Task HandleAsync_Success_DeletesBranchOverrideAndLogsAudit()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _notificationSettings.Verify(r => r.DeleteAsync(GuildId, GuildBranchId, GuildNotificationEventType.AbsenceAdded, default), Times.Once);
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsReset,
            It.Is<Dictionary<string, string>>(v => v["guildBranchId"] == GuildBranchId.ToString() && v["eventType"] == "AbsenceAdded"),
            default), Times.Once);
    }
}
