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

public class UpdateRaidSeriesCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidSeriesRepository> _raidSeriesRepository = new();
    private readonly Mock<IRaidZoneRepository> _raidZoneRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly UpdateRaidSeriesCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int SeriesId = 5;

    public UpdateRaidSeriesCommandHandlerTests()
    {
        _sut = new UpdateRaidSeriesCommandHandler(_access.Object, _raidSeriesRepository.Object, _raidZoneRepository.Object, _auditLogService.Object);
    }

    private static UpdateRaidSeriesCommand MakeCommand(int groupCount = 2, int slotsPerGroup = 5, List<int>? zoneIds = null, int intervalWeeks = 1) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        SeriesId = SeriesId,
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
    public async Task HandleAsync_SeriesNotFound_ReturnsRaidSeriesNotFound()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidSeriesRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidSeries>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidSeriesNotFound);
        _auditLogService.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NonPositiveIntervalWeeks_DefaultsToOne()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidSeriesRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidSeries>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand(intervalWeeks: -3));

        result.IsSuccess.Should().BeTrue();
        _raidSeriesRepository.Verify(r => r.UpdateAsync(It.Is<RaidSeries>(s => s.RecurrenceIntervalWeeks == 1), GuildBranchId, It.IsAny<IEnumerable<int>>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogsAudit()
    {
        SetupOfficer();
        _raidZoneRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync([new RaidZone { Id = 1 }]);
        _raidSeriesRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidSeries>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidSeriesRepository.Verify(r => r.UpdateAsync(
            It.Is<RaidSeries>(s => s.Id == SeriesId && s.Name == "Split 1" && s.GroupCount == 2 && s.SlotsPerGroup == 5),
            GuildBranchId, It.Is<IEnumerable<int>>(ids => ids.Single() == 1), default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(GuildId, RequesterId, GuildAuditAction.RaidSeriesUpdated, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }
}
