using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Series.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Series.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Series.CommandHandlers;

public class CreateRaidSeriesCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidSeriesRepository> _raidSeriesRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly CreateRaidSeriesCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public CreateRaidSeriesCommandHandlerTests()
    {
        _sut = new CreateRaidSeriesCommandHandler(_access.Object, _raidSeriesRepository.Object, _raidZoneRepository.Object, _auditLogService.Object);
    }

    private static CreateRaidSeriesCommand MakeCommand(int groupCount = 2, int slotsPerGroup = 5, List<int>? zoneIds = null, int intervalWeeks = 1) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        Name = "Split 1",
        RecurrenceDayOfWeek = DayOfWeek.Wednesday,
        RecurrenceStartTimeLocal = new TimeOnly(20, 0),
        RecurrenceIntervalWeeks = intervalWeeks,
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
    public async Task HandleAsync_UnknownZone_ReturnsRaidZoneNotFound()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidZoneNotFound);
    }

    [Fact]
    public async Task HandleAsync_NonPositiveIntervalWeeks_DefaultsToOne()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidSeriesRepository.Setup(r => r.AddAsync(It.IsAny<RaidSeries>(), default)).ReturnsAsync((RaidSeries s, CancellationToken _) => { s.Id = 5; return s; });

        var result = await _sut.HandleAsync(MakeCommand(intervalWeeks: 0));

        result.IsSuccess.Should().BeTrue();
        _raidSeriesRepository.Verify(r => r.AddAsync(It.Is<RaidSeries>(s => s.RecurrenceIntervalWeeks == 1), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesActiveSeriesAndLogsAudit()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidSeriesRepository.Setup(r => r.AddAsync(It.IsAny<RaidSeries>(), default)).ReturnsAsync((RaidSeries s, CancellationToken _) => { s.Id = 5; return s; });

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { Id = 5 });
        _raidSeriesRepository.Verify(r => r.AddAsync(It.Is<RaidSeries>(s =>
            s.GuildId == GuildId && s.GuildBranchId == GuildBranchId &&
            s.IsActive && s.SignupMode == SignupMode.DefaultPresent &&
            s.CreatedByDiscordId == RequesterId && s.DefaultZones.Count == 1),
            default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(GuildId, RequesterId, GuildAuditAction.RaidSeriesCreated, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }
}
