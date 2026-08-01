using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class PublishRaidEventCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly PublishRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public PublishRaidEventCommandHandlerTests()
    {
        _sut = new PublishRaidEventCommandHandler(_access.Object, _raidEventRepository.Object, _auditLogService.Object);
    }

    private static PublishRaidEventCommand MakeCommand() => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
    };

    private static RaidEvent MakeEvent(RaidPublicationStatus status = RaidPublicationStatus.Draft) => new()
    {
        Id = EventId,
        Name = "Split 1",
        StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc),
        PublicationStatus = status,
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
}
