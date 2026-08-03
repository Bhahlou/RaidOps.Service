using FluentAssertions;
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

public class PublishRaidEventCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly Mock<IGuildNotificationDispatcher> _guildNotificationDispatcher = new();
    private readonly Mock<IRaidNotificationContentBuilder> _raidNotificationContentBuilder = new();
    private readonly PublishRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public PublishRaidEventCommandHandlerTests()
    {
        _sut = new PublishRaidEventCommandHandler(
            _access.Object, _raidEventRepository.Object, _guildsRepository.Object, _auditLogService.Object,
            _guildNotificationDispatcher.Object, _raidNotificationContentBuilder.Object);
    }

    private static PublishRaidEventCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
    };

    private static RaidEvent MakeEvent(RaidPublicationStatus status = RaidPublicationStatus.Draft, List<RaidEventZone>? targetZones = null) => new()
    {
        Id = EventId,
        Name = "Split 1",
        StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc),
        PublicationStatus = status,
        TargetZones = targetZones ?? [],
    };

    private void SetupOfficer() =>
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_EventNotFound_ReturnsRaidEventNotFound()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync((RaidEvent?)null);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
    }

    [Fact]
    public async Task HandleAsync_AlreadyPublished_ReturnsRaidEventAlreadyPublished()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(RaidPublicationStatus.Published));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventAlreadyPublished);
        _raidEventRepository.Verify(r => r.PublishAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PublishRaceLostBetweenReadAndWrite_ReturnsRaidEventNotFound()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
        _raidEventRepository.Setup(r => r.PublishAsync(EventId, GuildBranchId, RequesterId, default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
        _auditLogService.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_PublishesAndLogsAudit()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent());
        _raidEventRepository.Setup(r => r.PublishAsync(EventId, GuildBranchId, RequesterId, default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventPublished,
            It.Is<Dictionary<string, string>>(d => d["eventName"] == "Split 1"), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsAuditWithGuildLocalTimeAndZoneNames()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(MakeEvent(targetZones:
        [
            new RaidEventZone { RaidZone = new RaidZone { Id = 1, Name = "Molten Core" } },
        ]));
        _raidEventRepository.Setup(r => r.PublishAsync(EventId, GuildBranchId, RequesterId, default)).ReturnsAsync(true);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Name = "G", Timezone = "Europe/Paris" });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _auditLogService.Verify(a => a.LogAsync(
            GuildId, RequesterId, GuildAuditAction.RaidEventPublished,
            It.Is<Dictionary<string, string>>(d =>
                d["eventName"] == "Split 1" &&
                d["startsAtLocal"] == "2026-02-01 21:00" &&
                d["raidZoneNames"] == "Molten Core"),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_BuildsAndDispatchesPublishedNotification()
    {
        SetupOfficer();
        var raidEvent = MakeEvent();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(raidEvent);
        _raidEventRepository.Setup(r => r.PublishAsync(EventId, GuildBranchId, RequesterId, default)).ReturnsAsync(true);
        var embed = new DiscordEmbedContent("Raid published");
        _raidNotificationContentBuilder.Setup(b => b.BuildPublishedAsync(GuildId, RequesterId, raidEvent, default)).ReturnsAsync(embed);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _guildNotificationDispatcher.Verify(d => d.NotifyAsync(GuildId, GuildNotificationEventType.RaidPublished, GuildBranchId, embed, default), Times.Once);
    }
}
