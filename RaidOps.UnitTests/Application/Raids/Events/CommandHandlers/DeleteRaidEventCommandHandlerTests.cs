using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class DeleteRaidEventCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IRaidEventDeletionNotifier> _raidEventDeletionNotifier = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<ILogger<DeleteRaidEventCommandHandler>> _logger = new();
    private readonly DeleteRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public DeleteRaidEventCommandHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new DeleteRaidEventCommandHandler(
            _access.Object, _raidEventRepository.Object, _guildsRepository.Object, _auditLogService.Object,
            _raidEventDeletionNotifier.Object, _discordBotService.Object, _logger.Object);
    }

    private static DeleteRaidEventCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidEventRepository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
        _raidEventRepository.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_DeletesAndLogsAudit()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Name = "Split 1" });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.DeleteAsync(EventId, GuildBranchId, default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventDeleted,
            It.Is<Dictionary<string, string>>(d => d["eventName"] == "Split 1"), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditWithGuildLocalTimeAndZoneNames()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            Name = "Split 1",
            StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc),
            TargetZones = [new RaidEventZone { RaidZone = new RaidZone { Id = 1, Name = "Molten Core" } }],
        });
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Timezone = "Europe/Paris" });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventDeleted,
            It.Is<Dictionary<string, string>>(d =>
                d["eventName"] == "Split 1" &&
                d["startsAtLocal"] == "2026-02-01 21:00" &&
                d["raidZoneNames"] == "Molten Core"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_NotifiesDeletionWithTheDeletedEvent()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        var existing = new RaidEvent { Id = EventId, Name = "Split 1", PublicationStatus = RaidPublicationStatus.Published };
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(existing);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidEventDeletionNotifier.Verify(n => n.NotifyAsync(GuildId, RequesterId, GuildBranchId, existing, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NotOfficer_DoesNotNotify()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        await _sut.HandleAsync(MakeCommand());

        _raidEventDeletionNotifier.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<RaidEvent>(), default), Times.Never);
    }

    // ── Bot-owned dedicated channel cleanup ──────────────────────────────────

    [Fact]
    public async Task HandleAsync_BotOwnedChannel_DeletesTheChannel()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, Name = "Split 1", DedicatedAnnouncementChannelId = "999", DedicatedAnnouncementChannelIsBotOwned = true,
        });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guildService.Verify(g => g.DeleteChannelAsync("999", default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NotBotOwnedChannel_NeverDeletesTheChannel()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, Name = "Split 1", DedicatedAnnouncementChannelId = "999", DedicatedAnnouncementChannelIsBotOwned = false,
        });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guildService.Verify(g => g.DeleteChannelAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoChannel_NeverCallsDeleteChannel()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Name = "Split 1" });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guildService.Verify(g => g.DeleteChannelAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChannelDeleteThrows_StillSucceeds()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId, Name = "Split 1", DedicatedAnnouncementChannelId = "999", DedicatedAnnouncementChannelIsBotOwned = true,
        });
        _guildService.Setup(g => g.DeleteChannelAsync("999", default)).ThrowsAsync(new InvalidOperationException("already gone"));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.DeleteAsync(EventId, GuildBranchId, default), Times.Once);
    }
}
