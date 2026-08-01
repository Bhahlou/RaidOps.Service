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
    private readonly Mock<IRaidGridAndZoneValidator> _gridAndZoneValidator = new();
    private readonly Mock<IRaidSeriesRepository> _raidSeriesRepository = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();
    private readonly CreateRaidSeriesCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    public CreateRaidSeriesCommandHandlerTests()
    {
        _sut = new CreateRaidSeriesCommandHandler(_gridAndZoneValidator.Object, _raidSeriesRepository.Object, _auditLogService.Object);

        _gridAndZoneValidator.Setup(v => v.ValidateAsync(RequesterId, GuildId, GuildBranchId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(Result<List<int>>.Ok([1]));
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

    [Fact]
    public async Task HandleAsync_ValidatorFails_PropagatesErrorWithoutPersisting()
    {
        _gridAndZoneValidator.Setup(v => v.ValidateAsync(RequesterId, GuildId, GuildBranchId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(Result<List<int>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch."));

        var result = await _sut.HandleAsync(MakeCommand());

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidSeriesRepository.Verify(r => r.AddAsync(It.IsAny<RaidSeries>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NonPositiveIntervalWeeks_DefaultsToOne()
    {
        _raidSeriesRepository.Setup(r => r.AddAsync(It.IsAny<RaidSeries>(), default)).ReturnsAsync((RaidSeries s, CancellationToken _) => { s.Id = 5; return s; });

        var result = await _sut.HandleAsync(MakeCommand(intervalWeeks: 0));

        result.IsSuccess.Should().BeTrue();
        _raidSeriesRepository.Verify(r => r.AddAsync(It.Is<RaidSeries>(s => s.RecurrenceIntervalWeeks == 1), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Success_CreatesActiveSeriesAndLogsAudit()
    {
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
