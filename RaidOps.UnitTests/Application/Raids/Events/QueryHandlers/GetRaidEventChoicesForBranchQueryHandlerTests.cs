using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.QueryHandlers;

public class GetRaidEventChoicesForBranchQueryHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IGuildBranchesRepository> _guildBranchesRepository = new();
    private readonly Mock<IWeeklyLockoutScheduleRepository> _weeklyLockoutScheduleRepository = new();
    private readonly Mock<IRaidLockoutService> _raidLockoutService = new();
    private readonly GetRaidEventChoicesForBranchQueryHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "officer-1";

    private static readonly DateTime AroundStartsAtUtc = new(2026, 2, 5, 20, 0, 0, DateTimeKind.Utc);

    public GetRaidEventChoicesForBranchQueryHandlerTests()
    {
        _sut = new GetRaidEventChoicesForBranchQueryHandler(
            _access.Object, _raidEventRepository.Object, _guildsRepository.Object, _guildBranchesRepository.Object,
            _weeklyLockoutScheduleRepository.Object, _raidLockoutService.Object);

        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Officer);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default)).ReturnsAsync([]);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
    }

    private static GetRaidEventChoicesForBranchQuery MakeQuery() => new()
    {
        GuildId = GuildId,
        GuildBranchId = GuildBranchId,
        RequesterDiscordId = RequesterId,
        AroundStartsAtUtc = AroundStartsAtUtc,
    };

    [Fact]
    public async Task HandleAsync_NotOfficer_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
        _raidEventRepository.Verify(r => r.GetForGuildBranchInRangeAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoRegionConfigured_UsesSixtyDayFallbackWindow()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = null });

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.GetForGuildBranchInRangeAsync(
            GuildBranchId, AroundStartsAtUtc.AddDays(-60), AroundStartsAtUtc.AddDays(60), default), Times.Once);
        _weeklyLockoutScheduleRepository.Verify(r => r.GetByRegionAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RegionButNoScheduleSeeded_UsesSixtyDayFallbackWindow()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync((WeeklyLockoutSchedule?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.GetForGuildBranchInRangeAsync(
            GuildBranchId, AroundStartsAtUtc.AddDays(-60), AroundStartsAtUtc.AddDays(60), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_GuildBranchNotFound_UsesSixtyDayFallbackWindow()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync((GuildBranch?)null);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.GetForGuildBranchInRangeAsync(
            GuildBranchId, AroundStartsAtUtc.AddDays(-60), AroundStartsAtUtc.AddDays(60), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RegionWithSchedule_ScopesToTheLockoutWindowAroundTheGivenDate()
    {
        var anchor = new DateTime(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc);
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = "eu" });
        _weeklyLockoutScheduleRepository.Setup(r => r.GetByRegionAsync("eu", default)).ReturnsAsync(new WeeklyLockoutSchedule { Region = "eu", AnchorUtc = anchor, CadenceDays = 7 });
        var windowStart = anchor.AddDays(35);
        _raidLockoutService.Setup(s => s.GetLockoutWindowStart(anchor, 7, It.IsAny<IReadOnlyCollection<RaidLockoutCadenceOverride>>(), AroundStartsAtUtc))
            .Returns(windowStart);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.GetForGuildBranchInRangeAsync(
            GuildBranchId, windowStart, windowStart.AddDays(7).AddTicks(-1), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MapsEveryFieldIncludingExtendsRaidEventId()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = null });
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = "Europe/Paris" });
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default)).ReturnsAsync(
        [
            new RaidEvent
            {
                Id = 77,
                GuildBranchId = GuildBranchId,
                Name = "Split 1",
                StartsAtUtc = new DateTime(2026, 2, 5, 20, 0, 0, DateTimeKind.Utc),
                ExtendsRaidEventId = 50,
                GuildBranch = new GuildBranch { Id = GuildBranchId, Branch = new Branch { Name = "Classic Era" } },
            },
        ]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var choice = result.Value!.Should().ContainSingle().Subject;
        choice.Id.Should().Be(77);
        choice.GuildBranchId.Should().Be(GuildBranchId);
        choice.Name.Should().Be("Split 1");
        choice.BranchName.Should().Be("Classic Era");
        choice.ExtendsRaidEventId.Should().Be(50);
        // 2026-02-05 20:00 UTC -> Europe/Paris is UTC+1 in February (no DST) -> 21:00 local.
        choice.StartsAtLocal.Should().Be(new DateTime(2026, 2, 5, 21, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_FallsBackToUtcForStartsAtLocal()
    {
        _guildBranchesRepository.Setup(r => r.GetByIdAsync(GuildBranchId, default)).ReturnsAsync(new GuildBranch { Id = GuildBranchId, GuildId = GuildId, Region = null });
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);
        var startsAtUtc = new DateTime(2026, 2, 5, 20, 0, 0, DateTimeKind.Utc);
        _raidEventRepository.Setup(r => r.GetForGuildBranchInRangeAsync(GuildBranchId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default)).ReturnsAsync(
        [
            new RaidEvent
            {
                Id = 77,
                GuildBranchId = GuildBranchId,
                Name = "Split 1",
                StartsAtUtc = startsAtUtc,
                GuildBranch = new GuildBranch { Id = GuildBranchId, Branch = new Branch { Name = "Classic Era" } },
            },
        ]);

        var result = await _sut.HandleAsync(MakeQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle().Which.StartsAtLocal.Should().Be(startsAtUtc);
    }
}
