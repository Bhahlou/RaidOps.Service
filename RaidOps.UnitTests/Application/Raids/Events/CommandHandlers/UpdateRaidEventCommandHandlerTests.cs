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

public class UpdateRaidEventCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly UpdateRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int EventId = 5;

    public UpdateRaidEventCommandHandlerTests()
    {
        _sut = new UpdateRaidEventCommandHandler(_access.Object, _raidEventRepository.Object, _raidZoneRepository.Object, _auditLogService.Object);
    }

    private static UpdateRaidEventCommand MakeCommand(int groupCount = 2, int slotsPerGroup = 5, List<int>? zoneIds = null) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        EventId = EventId,
        Name = "Split 1",
        StartsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc),
        GroupCount = groupCount,
        SlotsPerGroup = slotsPerGroup,
        RaidZoneIds = zoneIds ?? [1],
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

    [Theory]
    [InlineData(0, 5)]
    [InlineData(2, 0)]
    public async Task HandleAsync_NonPositiveGridShape_ReturnsInvalidRequest(int groupCount, int slotsPerGroup)
    {
        SetupOfficer();

        var result = await _sut.HandleAsync(MakeCommand(groupCount, slotsPerGroup));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_NoZonesTargeted_ReturnsInvalidRequest()
    {
        SetupOfficer();

        var result = await _sut.HandleAsync(MakeCommand(zoneIds: []));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
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
    public async Task HandleAsync_ShrinkingGridBelowExistingAssignment_ReturnsGridShrinkWouldOrphanAssignments()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            Assignments = [new RaidSlotAssignment { GroupNumber = 3, SlotNumber = 1 }],
        });

        // Shrinking to 2 groups would orphan the assignment sitting in group 3.
        var result = await _sut.HandleAsync(MakeCommand(groupCount: 2, slotsPerGroup: 5));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GridShrinkWouldOrphanAssignments);
        _raidZoneRepository.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShrinkingSlotsPerGroupBelowExistingAssignment_ReturnsGridShrinkWouldOrphanAssignments()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent
        {
            Id = EventId,
            Assignments = [new RaidSlotAssignment { GroupNumber = 1, SlotNumber = 8 }],
        });

        var result = await _sut.HandleAsync(MakeCommand(groupCount: 2, slotsPerGroup: 5));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GridShrinkWouldOrphanAssignments);
    }

    [Fact]
    public async Task HandleAsync_UnknownZone_ReturnsRaidZoneNotFound()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidZoneNotFound);
    }

    [Fact]
    public async Task HandleAsync_UpdateRaceLostBetweenReadAndWrite_ReturnsRaidEventNotFound()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidEventNotFound);
        _auditLogService.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogsAudit()
    {
        SetupOfficer();
        _raidEventRepository.Setup(r => r.GetByIdAsync(EventId, GuildBranchId, default)).ReturnsAsync(new RaidEvent { Id = EventId, Assignments = [] });
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidEvent>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.UpdateAsync(
            It.Is<RaidEvent>(e => e.Id == EventId && e.Name == "Split 1" && e.GroupCount == 2 && e.SlotsPerGroup == 5),
            GuildBranchId, It.Is<IEnumerable<int>>(ids => ids.Single() == 1), default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(GuildId, RequesterId, GuildAuditAction.RaidEventUpdated, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }
}
