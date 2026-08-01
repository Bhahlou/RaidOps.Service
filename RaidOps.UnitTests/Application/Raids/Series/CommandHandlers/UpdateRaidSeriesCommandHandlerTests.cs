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
    private readonly Mock<IRaidGridAndZoneValidator> _gridAndZoneValidator = new();
    private readonly Mock<IRaidSeriesRepository> _raidSeriesRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly UpdateRaidSeriesCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";
    private const int SeriesId = 5;

    public UpdateRaidSeriesCommandHandlerTests()
    {
        _sut = new UpdateRaidSeriesCommandHandler(_gridAndZoneValidator.Object, _raidSeriesRepository.Object, _auditLogService.Object);

        _gridAndZoneValidator.Setup(v => v.ValidateAsync(RequesterId, GuildId, GuildBranchId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(Result<List<int>>.Ok([1]));
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

    [Fact]
    public async Task HandleAsync_ValidatorFails_PropagatesErrorWithoutPersisting()
    {
        _gridAndZoneValidator.Setup(v => v.ValidateAsync(RequesterId, GuildId, GuildBranchId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(Result<List<int>>.Fail(ResponseDetail.InvalidRequest, "GroupCount and SlotsPerGroup must be positive."));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
        _raidSeriesRepository.Verify(r => r.UpdateAsync(It.IsAny<RaidSeries>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SeriesNotFound_ReturnsRaidSeriesNotFound()
    {
        _raidSeriesRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidSeries>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(false);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.RaidSeriesNotFound);
        _auditLogService.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GuildAuditAction>(), It.IsAny<Dictionary<string, string>>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NonPositiveIntervalWeeks_DefaultsToOne()
    {
        _raidSeriesRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidSeries>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand(intervalWeeks: -3));

        result.IsSuccess.Should().BeTrue();
        _raidSeriesRepository.Verify(r => r.UpdateAsync(It.Is<RaidSeries>(s => s.RecurrenceIntervalWeeks == 1), GuildBranchId, It.IsAny<IEnumerable<int>>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_UpdatesAndLogsAudit()
    {
        _raidSeriesRepository.Setup(r => r.UpdateAsync(It.IsAny<RaidSeries>(), GuildBranchId, It.IsAny<IEnumerable<int>>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsSuccess.Should().BeTrue();
        _raidSeriesRepository.Verify(r => r.UpdateAsync(
            It.Is<RaidSeries>(s => s.Id == SeriesId && s.Name == "Split 1" && s.GroupCount == 2 && s.SlotsPerGroup == 5),
            GuildBranchId, It.Is<IEnumerable<int>>(ids => ids.Single() == 1), default), Times.Once);
        _auditLogService.Verify(a => a.LogAsync(GuildId, RequesterId, GuildAuditAction.RaidSeriesUpdated, It.IsAny<Dictionary<string, string>>(), default), Times.Once);
    }
}
