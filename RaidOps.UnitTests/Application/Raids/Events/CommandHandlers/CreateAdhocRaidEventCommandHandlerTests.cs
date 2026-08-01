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

public class CreateAdhocRaidEventCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly CreateAdhocRaidEventCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public CreateAdhocRaidEventCommandHandlerTests()
    {
        _sut = new CreateAdhocRaidEventCommandHandler(_access.Object, _raidEventRepository.Object, _raidZoneRepository.Object, _auditLogService.Object);
    }

    private static CreateAdhocRaidEventCommand MakeCommand(int groupCount = 2, int slotsPerGroup = 5, List<int>? zoneIds = null) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        Name = "One-shot Kara clear",
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
    [InlineData(-1, 5)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
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
    public async Task HandleAsync_UnknownZone_ReturnsRaidZoneNotFound()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(zoneIds: [1, 2]));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidZoneNotFound);
    }

    [Fact]
    public async Task HandleAsync_DuplicateZoneIds_AreDeduplicatedBeforeLookup()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1 && ids.Contains(1)), default))
            .ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });

        var result = await _sut.HandleAsync(MakeCommand(zoneIds: [1, 1]));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesDraftScheduledEventAndLogsAudit()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default))
            .ReturnsAsync((RaidEvent e, CancellationToken _) => { e.Id = 77; return e; });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { Id = 77 });
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.GuildId == GuildId &&
            e.GuildBranchId == GuildBranchId &&
            e.RaidSeriesId == null &&
            e.Status == RaidEventStatus.Scheduled &&
            e.PublicationStatus == RaidPublicationStatus.Draft &&
            e.SignupMode == SignupMode.DefaultPresent &&
            e.CreatedByDiscordId == RequesterId &&
            e.TargetZones.Count == 1),
            default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(GuildId, RequesterId, GuildAuditAction.RaidEventCreated, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }
}
