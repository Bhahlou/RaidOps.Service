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

public class UpdateGuildNotificationSettingsCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guilds = new();
    private readonly Mock<IGuildNotificationSettingsRepository> _notificationSettings = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly UpdateGuildNotificationSettingsCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const string RequesterId = "user-1";

    public UpdateGuildNotificationSettingsCommandHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new UpdateGuildNotificationSettingsCommandHandler(
            _access.Object, _guilds.Object, _notificationSettings.Object, _discordBotService.Object, _auditLog.Object);
    }

    private static UpdateGuildNotificationSettingsCommand MakeCommand(List<GuildNotificationSettingInput> settings) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        Settings = settings,
    };

    private void SetupOfficerOnRegisteredGuild()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", IsRegistered = true });
    }

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
    public async Task HandleAsync_Success_UpsertsSettingsNullingChannelWhenDisabled()
    {
        SetupOfficerOnRegisteredGuild();
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, null, default)).ReturnsAsync([]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns([]);

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
            // Disabled but with a leftover ChannelId from a prior save — must be nulled on persist.
            new() { EventType = GuildNotificationEventType.AbsenceRemoved, Enabled = false, ChannelId = "222" },
        };

        var result = await _sut.HandleAsync(MakeCommand(settings));

        result.IsSuccess.Should().BeTrue();

        _notificationSettings.Verify(r => r.UpsertRangeAsync(
            GuildId,
            null,
            It.Is<IEnumerable<GuildNotificationSetting>>(rows =>
                rows.Any(x => x.EventType == GuildNotificationEventType.AbsenceAdded && x.Enabled && x.ChannelId == "111") &&
                rows.Any(x => x.EventType == GuildNotificationEventType.AbsenceRemoved && !x.Enabled && x.ChannelId == null)),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EventNewlyEnabled_LogsChangedEventWithResolvedChannelName()
    {
        SetupOfficerOnRegisteredGuild();
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, null, default)).ReturnsAsync([]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns(
        [
            new DiscordChannelInfo(111, "general", [], null),
        ]);

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
        };

        var result = await _sut.HandleAsync(MakeCommand(settings));

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["changedEvents"] == "AbsenceAdded" &&
                v["oldAbsenceAddedEnabled"] == "false" &&
                v["newAbsenceAddedEnabled"] == "true" &&
                v["newAbsenceAddedChannelName"] == "general" &&
                !v.ContainsKey("oldAbsenceAddedChannelName")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EventDisabled_LogsOldChannelNameAndNoNewChannelName()
    {
        SetupOfficerOnRegisteredGuild();
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, null, default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.AbsenceRemoved, Enabled = true, ChannelId = "222" },
        ]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns(
        [
            new DiscordChannelInfo(222, "mod-log", [], null),
        ]);

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceRemoved, Enabled = false, ChannelId = null },
        };

        var result = await _sut.HandleAsync(MakeCommand(settings));

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsUpdated,
            It.Is<Dictionary<string, string>>(v =>
                v["changedEvents"] == "AbsenceRemoved" &&
                v["oldAbsenceRemovedEnabled"] == "true" &&
                v["newAbsenceRemovedEnabled"] == "false" &&
                v["oldAbsenceRemovedChannelName"] == "mod-log" &&
                !v.ContainsKey("newAbsenceRemovedChannelName")),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChannelIdUnresolvedInBotCache_FallsBackToRawChannelId()
    {
        SetupOfficerOnRegisteredGuild();
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, null, default)).ReturnsAsync([]);
        // The channel was deleted/renamed on Discord since it was configured — not in the live cache.
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns([]);

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "999" },
        };

        var result = await _sut.HandleAsync(MakeCommand(settings));

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsUpdated,
            It.Is<Dictionary<string, string>>(v => v["newAbsenceAddedChannelName"] == "999"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BotNotInGuildCache_StillSucceedsAndFallsBackToRawChannelId()
    {
        SetupOfficerOnRegisteredGuild();
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, null, default)).ReturnsAsync([]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Throws<InvalidOperationException>();

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
        };

        var result = await _sut.HandleAsync(MakeCommand(settings));

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.NotificationSettingsUpdated,
            It.Is<Dictionary<string, string>>(v => v["newAbsenceAddedChannelName"] == "111"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NothingActuallyChanged_DoesNotLogAudit()
    {
        SetupOfficerOnRegisteredGuild();
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, null, default)).ReturnsAsync(
        [
            new GuildNotificationSetting { GuildId = GuildId, EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
        ]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns([]);

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
        };

        var result = await _sut.HandleAsync(MakeCommand(settings));

        result.IsSuccess.Should().BeTrue();
        _auditLog.Verify(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task HandleAsync_BranchScoped_Success_UpsertsSettingsForThatBranchUsingBranchEffectiveState()
    {
        const int guildBranchId = 7;
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, guildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _guilds.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", IsRegistered = true });
        _notificationSettings.Setup(r => r.GetEffectiveForGuildAsync(GuildId, guildBranchId, default)).ReturnsAsync([]);
        _guildService.Setup(s => s.GetChannels(GuildId, default)).Returns([]);

        var settings = new List<GuildNotificationSettingInput>
        {
            new() { EventType = GuildNotificationEventType.AbsenceAdded, Enabled = true, ChannelId = "111" },
        };
        var command = MakeCommand(settings);
        command.GuildBranchId = guildBranchId;

        var result = await _sut.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();

        _notificationSettings.Verify(r => r.UpsertRangeAsync(
            GuildId,
            guildBranchId,
            It.Is<IEnumerable<GuildNotificationSetting>>(rows =>
                rows.Any(x => x.EventType == GuildNotificationEventType.AbsenceAdded && x.Enabled && x.ChannelId == "111" && x.GuildBranchId == guildBranchId)),
            default), Times.Once);
        _notificationSettings.Verify(r => r.GetEffectiveForGuildAsync(GuildId, guildBranchId, default), Times.Once);
    }
}
