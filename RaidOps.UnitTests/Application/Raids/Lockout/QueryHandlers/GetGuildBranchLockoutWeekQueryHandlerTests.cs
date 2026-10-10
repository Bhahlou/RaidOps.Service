using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Lockout.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Lockout.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Lockout.QueryHandlers;

public class GetGuildBranchLockoutWeekQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IWeeklyLockoutScheduleRepository> _weeklyLockoutScheduleRepository = new();
    private readonly Mock<IRaidLockoutService> _raidLockoutService = new();
    private readonly GetGuildBranchLockoutWeekQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";

    public GetGuildBranchLockoutWeekQueryHandlerTests()
    {
        _sut = new GetGuildBranchLockoutWeekQueryHandler(_access.Object, _guildsRepository.Object, _guildBranchesRepository.Object, _weeklyLockoutScheduleRepository.Object, _raidLockoutService.Object);
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
    }

    private static GetGuildBranchLockoutWeekQuery MakeQuery() => new()
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
    public async Task HandleAsync_GuildBranchNotFound_ReturnsGuildBranchNotFound()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_GuildBranchBelongsToDifferentGuild_ReturnsGuildBranchNotFound()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = "some-other-guild" });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildBranchNotFound);
    }

    [Fact]
    public async Task HandleAsync_NoRegionConfigured_ReturnsNullWeek()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = null });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WeekStartLocal.Should().BeNull();
        result.Value.WeekEndLocal.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_NoScheduleSeededForRegion_ReturnsNullWeek()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync((WeeklyLockoutSchedule?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WeekStartLocal.Should().BeNull();
        result.Value.WeekEndLocal.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ScheduleFound_ReturnsResolvedWeekInGuildLocalDates()
    {
        var anchor = new DateTime(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc);
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync(new WeeklyLockoutSchedule { Region = "eu", AnchorUtc = anchor, CadenceDays = 7 });
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(anchor, 7, It.Is<IReadOnlyCollection<RaidLockoutCadenceOverride>>(o => o.Count == 0), It.IsAny<DateTime>()))
            .Returns(anchor.AddDays(14));
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        // windowStart = anchor + 14 days; windowEnd = windowStart + 7 days - 1 tick, i.e. the day before the next reset.
        result.Value!.WeekStartLocal.Should().Be(DateOnly.FromDateTime(anchor.AddDays(14)));
        result.Value.WeekEndLocal.Should().Be(DateOnly.FromDateTime(anchor.AddDays(21)));
    }

    [Fact]
    public async Task HandleAsync_ScheduleFoundButGuildMissing_StillResolvesUsingUtcFallback()
    {
        var anchor = new DateTime(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc);
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync(new WeeklyLockoutSchedule { Region = "eu", AnchorUtc = anchor, CadenceDays = 7 });
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(anchor, 7, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), It.IsAny<DateTime>())).Returns(anchor);
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WeekStartLocal.Should().Be(DateOnly.FromDateTime(anchor));
    }
}
