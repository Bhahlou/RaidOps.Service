using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.CommandHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Events.CommandHandlers;

public class MaterializeRaidSeriesOccurrencesCommandHandlerTests
{
    private readonly Mock<IGuildAccessService> _access = new();
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IRaidSeriesRepository> _raidSeriesRepository = new();
    private readonly Mock<IRaidEventRepository> _raidEventRepository = new();
    private readonly Mock<IRaidSignupAnnouncementService> _raidSignupAnnouncementService = new();
    private readonly Mock<IDiscordBotService> _discordBotService = new();
    private readonly Mock<IGuildService> _guildService = new();
    private readonly Mock<ILogger<MaterializeRaidSeriesOccurrencesCommandHandler>> _logger = new();
    private readonly MaterializeRaidSeriesOccurrencesCommandHandler _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string RequesterId = "roster-1";

    public MaterializeRaidSeriesOccurrencesCommandHandlerTests()
    {
        _discordBotService.Setup(d => d.Guilds).Returns(_guildService.Object);
        _sut = new MaterializeRaidSeriesOccurrencesCommandHandler(
            _access.Object, _guildsRepository.Object, _raidSeriesRepository.Object, _raidEventRepository.Object, _raidSignupAnnouncementService.Object,
            _discordBotService.Object, _logger.Object);
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Roster);
        _raidEventRepository.Setup(r => r.ExistsForSeriesAndDateAsync(It.IsAny<int>(), It.IsAny<DateTime>(), default)).ReturnsAsync(false);
        _raidEventRepository.Setup(r => r.AddAsync(It.IsAny<RaidEvent>(), default)).ReturnsAsync((RaidEvent e, CancellationToken _) => e);
    }

    private static MaterializeRaidSeriesOccurrencesCommand MakeCommand(DateOnly rangeStart, DateOnly rangeEnd) => new()
    {
        GuildId = GuildId,
        RequesterDiscordId = RequesterId,
        GuildBranchId = GuildBranchId,
        RangeStart = rangeStart,
        RangeEnd = rangeEnd,
    };

    private static RaidSeries MakeWeeklySeries(DateTime createdAt, bool isActive = true) => new()
    {
        Id = 1,
        RecurrenceDayOfWeek = DayOfWeek.Wednesday,
        RecurrenceStartTimeLocal = new TimeOnly(20, 0),
        RecurrenceIntervalWeeks = 1,
        GroupCount = 2,
        SlotsPerGroup = 5,
        SignupMode = SignupMode.DefaultPresent,
        IsActive = isActive,
        CreatedAt = createdAt,
        DefaultZones = [new RaidSeriesZone { RaidZoneId = 7 }],
    };

    [Fact]
    public async Task HandleAsync_BelowRosterAccess_ReturnsForbidden()
    {
        _access.Setup(a => a.GetAccessLevelAsync(RequesterId, GuildId, GuildBranchId, default)).ReturnsAsync(GuildAccessLevel.Public);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Forbidden);
    }

    [Fact]
    public async Task HandleAsync_RangeEndBeforeRangeStart_ReturnsInvalidRequest()
    {
        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 11), new DateOnly(2026, 1, 5)));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.InvalidRequest);
    }

    [Fact]
    public async Task HandleAsync_GuildNotFound_ReturnsGuildNotFound()
    {
        _guildsRepository.Setup(g => g.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.GuildNotFound);
    }

    [Fact]
    public async Task HandleAsync_NoActiveSeries_MaterializesNothing()
    {
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 0 });
        _raidEventRepository.Verify(r => r.AddAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InactiveSeriesIsSkipped()
    {
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([MakeWeeklySeries(new DateTime(2026, 1, 7), isActive: false)]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.AddAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RangeWithNoMatchingWeekday_MaterializesNothing()
    {
        // Series recurs on Wednesday; range only covers Monday-Tuesday.
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([MakeWeeklySeries(new DateTime(2026, 1, 7))]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 6)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 0 });
        _raidEventRepository.Verify(r => r.AddAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlreadyMaterializedOccurrence_IsSkippedIdempotently()
    {
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([MakeWeeklySeries(new DateTime(2026, 1, 7))]);
        _raidEventRepository.Setup(r => r.ExistsForSeriesAndDateAsync(1, It.IsAny<DateTime>(), default)).ReturnsAsync(true);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 0 });
        _raidEventRepository.Verify(r => r.AddAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WeeklySeries_MaterializesOneEventPerMatchingWeekWithExpectedFields()
    {
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([MakeWeeklySeries(new DateTime(2026, 1, 7))]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 1 });
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.GuildId == GuildId &&
            e.GuildBranchId == GuildBranchId &&
            e.RaidSeriesId == 1 &&
            e.StartsAtUtc == new DateTime(2026, 1, 7, 20, 0, 0, DateTimeKind.Utc) &&
            e.GroupCount == 2 && e.SlotsPerGroup == 5 &&
            e.SignupMode == SignupMode.DefaultPresent &&
            e.Status == RaidEventStatus.Scheduled &&
            e.PublicationStatus == RaidPublicationStatus.Draft &&
            e.CreatedByDiscordId == RequesterId &&
            e.TargetZones.Single().RaidZoneId == 7),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WeeklySeriesOverMultipleWeeks_MaterializesOneEventPerWeek()
    {
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default))
            .ReturnsAsync([MakeWeeklySeries(new DateTime(2026, 1, 7))]);

        // Jan 5 - Jan 25 spans three Wednesdays: 7, 14, 21.
        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 25)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 3 });
        _raidEventRepository.Verify(r => r.AddAsync(It.IsAny<RaidEvent>(), default), Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_BiWeeklySeries_SkipsTheOffWeekRelativeToCreationWeek()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        series.RecurrenceIntervalWeeks = 2;
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);

        // Jan 7 (creation week) and Jan 21 (two weeks later) occur; Jan 14 (one week later) is skipped.
        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 25)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 2 });
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.StartsAtUtc == new DateTime(2026, 1, 7, 20, 0, 0, DateTimeKind.Utc)), default), Times.Once);
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.StartsAtUtc == new DateTime(2026, 1, 21, 20, 0, 0, DateTimeKind.Utc)), default), Times.Once);
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.StartsAtUtc == new DateTime(2026, 1, 14, 20, 0, 0, DateTimeKind.Utc)), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NonPositiveIntervalWeeks_TreatedAsEveryWeek()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        series.RecurrenceIntervalWeeks = 0;
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 25)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 3 });
    }

    [Fact]
    public async Task HandleAsync_MultipleActiveSeries_MaterializesEachIndependently()
    {
        var seriesA = MakeWeeklySeries(new DateTime(2026, 1, 7));
        var seriesB = MakeWeeklySeries(new DateTime(2026, 1, 7));
        seriesB.Id = 2;
        seriesB.RecurrenceDayOfWeek = DayOfWeek.Friday;
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([seriesA, seriesB]);

        // Jan 5 - Jan 11 contains one Wednesday (series A) and one Friday (series B).
        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 2 });
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.RaidSeriesId == 1), default), Times.Once);
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e => e.RaidSeriesId == 2), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SignupModeSeries_PublishesTheSignupCallForTheMaterializedOccurrence()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        series.SignupMode = SignupMode.Signup;
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DefaultPresentSeries_NeverPublishesASignupCall()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        _raidSignupAnnouncementService.Verify(s => s.PublishOrUpdateSignupCallAsync(It.IsAny<RaidEvent>(), default), Times.Never);
    }

    // ── Per-occurrence dedicated channel ─────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SeriesWithoutCategory_UsesSeriesSharedChannel()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        series.DedicatedAnnouncementChannelId = "shared-channel";
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.DedicatedAnnouncementChannelId == "shared-channel" && !e.DedicatedAnnouncementChannelIsBotOwned),
            default), Times.Once);
        _guildService.Verify(g => g.CreateTextChannelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SeriesWithCategory_CreatesAFreshChannelForTheOccurrence()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        series.Name = "Split 1";
        series.DedicatedAnnouncementChannelCategoryId = "cat-1";
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);
        _guildService.Setup(g => g.CreateTextChannelAsync(GuildId, It.IsAny<string>(), "cat-1", default))
            .ReturnsAsync(new DiscordChannelInfo(555, "split-1-wed-7-jan", []));

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        _guildService.Verify(g => g.CreateTextChannelAsync(GuildId, It.IsAny<string>(), "cat-1", default), Times.Once);
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.DedicatedAnnouncementChannelId == "555" && e.DedicatedAnnouncementChannelIsBotOwned),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChannelCreationFails_StillMaterializesWithNoChannel()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        series.DedicatedAnnouncementChannelCategoryId = "cat-1";
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);
        _guildService.Setup(g => g.CreateTextChannelAsync(GuildId, It.IsAny<string>(), "cat-1", default))
            .ThrowsAsync(new InvalidOperationException("missing permission"));

        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 11)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().BeEquivalentTo(new { materializedCount = 1 });
        _raidEventRepository.Verify(r => r.AddAsync(It.Is<RaidEvent>(e =>
            e.DedicatedAnnouncementChannelId == null && !e.DedicatedAnnouncementChannelIsBotOwned),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SeriesWithCategoryOverMultipleWeeks_CreatesOneChannelPerOccurrence()
    {
        var series = MakeWeeklySeries(new DateTime(2026, 1, 7));
        series.DedicatedAnnouncementChannelCategoryId = "cat-1";
        _raidSeriesRepository.Setup(r => r.GetByGuildBranchIdAsync(GuildBranchId, default)).ReturnsAsync([series]);
        _guildService.Setup(g => g.CreateTextChannelAsync(GuildId, It.IsAny<string>(), "cat-1", default))
            .ReturnsAsync(new DiscordChannelInfo(555, "chan", []));

        // Jan 5 - Jan 25 spans three Wednesdays: 7, 14, 21 — each occurrence should get its own create call.
        var result = await _sut.HandleAsync(MakeCommand(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 25)));

        result.IsSuccess.Should().BeTrue();
        _guildService.Verify(g => g.CreateTextChannelAsync(GuildId, It.IsAny<string>(), "cat-1", default), Times.Exactly(3));
    }
}
