using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Series.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Series.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Series.QueryHandlers;

public class GetRaidSeriesListQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidSeriesRepository> _raidSeriesRepository = new();
    private readonly GetRaidSeriesListQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";

    public GetRaidSeriesListQueryHandlerTests()
    {
        _sut = new GetRaidSeriesListQueryHandler(_access.Object, _raidSeriesRepository.Object);
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
    }

    private static GetRaidSeriesListQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
    };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_NoSeries_ReturnsEmptyList()
    {
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MapsSeriesFieldsIncludingInactiveOnes()
    {
        var series = new RaidSeries
        {
            Id = 5,
            Name = "Split 1",
            GuildBranch = new GuildBranch { Id = GuildBranchId, GuildId = GuildId, BranchId = 2, Branch = new Branch { Id = 2, Name = "TBC Classic" } },
            RecurrenceDayOfWeek = DayOfWeek.Wednesday,
            RecurrenceStartTimeLocal = new TimeOnly(20, 0),
            RecurrenceIntervalWeeks = 2,
            GroupCount = 2,
            SlotsPerGroup = 5,
            SignupMode = SignupMode.DefaultPresent,
            IsActive = false,
            DefaultZones = [new RaidSeriesZone { RaidZoneId = 7, RaidZone = new RaidZone { Id = 7, Name = "Serpentshrine Cavern", ShortCode = "SSC" } }],
        };
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value!.Single();
        response.Id.Should().Be(5);
        response.BranchId.Should().Be(2);
        response.BranchName.Should().Be("TBC Classic");
        response.IsActive.Should().BeFalse();
        response.RecurrenceIntervalWeeks.Should().Be(2);
        response.RaidZones.Should().ContainSingle(z => z.Id == 7 && z.Name == "Serpentshrine Cavern" && z.ShortCode == "SSC");
    }
}
