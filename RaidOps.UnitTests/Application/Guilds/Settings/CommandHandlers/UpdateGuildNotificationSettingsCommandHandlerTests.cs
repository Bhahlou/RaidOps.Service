using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Settings.CommandHandlers;

public class UpdateGuildNotificationSettingsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guilds = new();
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettings = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly UpdateGuildNotificationSettingsCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";

    public UpdateGuildNotificationSettingsCommandHandlerTests()
    {
        _sut = new UpdateGuildNotificationSettingsCommandHandler(_access.Object, _guilds.Object, _notificationSettings.Object, _auditLog.Object);
    }

    private static UpdateGuildNotificationSettingsCommand MakeCommand(List<GuildNotificationSettingInput> settings) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        Settings = settings,
    };

    [Fact]
    public async Task HandleAsync_RequesterNotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand([]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(MakeCommand([]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildNotRegistered_ReturnsGuildNotRegistered()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", IsRegistered = false });

        var result = await _sut.HandleAsync(MakeCommand([]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotRegistered);
    }

    [Fact]
    public async Task HandleAsync_Success_UpsertsSettingsNullingChannelWhenDisabledAndLogsAudit()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", IsRegistered = true });

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-1" },
            // Disabled but with a leftover ChannelId from a prior save — must be nulled on persist.
            new() { EventType = GuildNotificationEventType.AbsenceRemoved, Enabled = false, ChannelId = "chan-2" },
        };

        var result = await _sut.HandleAsync(MakeCommand(settings));

        result.IsSuccess.Should().BeTrue();

        _notificationSettings.Verify(r => r.UpsertRangeAsync(
            GuildId,
            null,
            It.Is<IEnumerable<GuildNotificationSetting>>(rows =>
                rows.Any(x => x.EventType == GuildNotificationEventType.AbsenceAdded && x.Enabled && x.ChannelId == "chan-1") &&
                rows.Any(x => x.EventType == GuildNotificationEventType.AbsenceRemoved && !x.Enabled && x.ChannelId == null)),
            default), Times.Once);

        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsUpdated,
            It.Is<Dictionary<string, string>>(v => v["eventCount"] == "2"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BranchScoped_RequesterNotBranchOfficer_ReturnsForbidden()
    {
        const int guildBranchId = 7;
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, guildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var command = MakeCommand([]);
        command.GuildBranchId = guildBranchId;

        var result = await _sut.HandleAsync(command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_BranchScoped_Success_UpsertsSettingsForThatBranch()
    {
        const int guildBranchId = 7;
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, guildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", IsRegistered = true });

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "chan-1" },
        };
        var command = MakeCommand(settings);
        command.GuildBranchId = guildBranchId;

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();

        _notificationSettings.Verify(r => r.UpsertRangeAsync(
            GuildId,
            guildBranchId,
            It.Is<IEnumerable<GuildNotificationSetting>>(rows =>
                rows.Any(x => x.EventType == GuildNotificationEventType.AbsenceAdded && x.Enabled && x.ChannelId == "chan-1" && x.GuildBranchId == guildBranchId)),
            default), Times.Once);
    }
}
