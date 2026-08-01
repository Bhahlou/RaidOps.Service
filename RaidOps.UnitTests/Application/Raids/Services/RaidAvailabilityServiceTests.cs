using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidAvailabilityServiceTests
{
    private readonly Mock<IGuildsRepository> _guildsRepository = new();
    private readonly Mock<IAvailabilityRepository> _availabilityRepository = new();
    private readonly Mock<IAvailabilityResolutionService> _availabilityResolutionService = new();
    private readonly RaidAvailabilityService _sut;

    private const string GuildId = "guild-1";
    private const int GuildBranchId = 10;
    private const string PlayerDiscordId = "player-1";

    private static readonly DateTime EventStartsAtUtc = new(2026, 2, 4, 20, 0, 0, DateTimeKind.Utc);

    public RaidAvailabilityServiceTests()
    {
        _sut = new RaidAvailabilityService(_guildsRepository.Object, _availabilityRepository.Object, _availabilityResolutionService.Object);

        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync(new Guild { Id = GuildId, Timezone = null });
        _availabilityRepository.Setup(r => r.GetExceptionsOverlappingForUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default)).ReturnsAsync([]);
        _availabilityRepository.Setup(r => r.GetPatternsForUsersAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync([]);
    }

    private void SetupResolvedDay(DayAvailabilityStatus status, TimeOnly? from = null, TimeOnly? until = null) =>
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([new ResolvedDayAvailabilityResponse { Status = status, AvailableFrom = from, AvailableUntil = until }]);

    // ── IsPlayerUnavailableAsync ─────────────────────────────────────────────

    [Fact]
    public async Task IsPlayerUnavailableAsync_Absent_ReturnsTrue()
    {
        SetupResolvedDay(DayAvailabilityStatus.Absent);

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_PartialOutsideWindow_ReturnsTrue()
    {
        // Event starts 20:00 UTC; member only available until 18:00.
        SetupResolvedDay(DayAvailabilityStatus.Partial, until: new TimeOnly(18, 0));

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_PartialWithinWindow_ReturnsFalse()
    {
        SetupResolvedDay(DayAvailabilityStatus.Partial, from: new TimeOnly(18, 0), until: new TimeOnly(23, 0));

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_PartialOpenEndedFromBeforeEvent_ReturnsFalse()
    {
        // No AvailableUntil bound — available indefinitely from 18:00 onward. Event starts 20:00.
        SetupResolvedDay(DayAvailabilityStatus.Partial, from: new TimeOnly(18, 0));

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_PartialOpenEndedFromAfterEvent_ReturnsTrue()
    {
        // No AvailableUntil bound, but the member isn't available until 21:00 — after the 20:00 event.
        SetupResolvedDay(DayAvailabilityStatus.Partial, from: new TimeOnly(21, 0));

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_PartialWithNoBoundsAtAll_ReturnsFalse()
    {
        SetupResolvedDay(DayAvailabilityStatus.Partial);

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_Available_ReturnsFalse()
    {
        SetupResolvedDay(DayAvailabilityStatus.Available);

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_NoDeclarationsResolved_ReturnsFalse()
    {
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([]);

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsPlayerUnavailableAsync_GuildNotFound_StillResolvesUsingUtcAsLocalFallback()
    {
        _guildsRepository.Setup(r => r.GetByIdAsync(GuildId, default)).ReturnsAsync((Guild?)null);
        SetupResolvedDay(DayAvailabilityStatus.Absent);

        var result = await _sut.IsPlayerUnavailableAsync(PlayerDiscordId, GuildId, GuildBranchId, EventStartsAtUtc);

        result.Should().BeTrue();
    }

    // ── LoadRosterAvailabilityAsync ──────────────────────────────────────────

    [Fact]
    public async Task LoadRosterAvailabilityAsync_QueriesBothRepositoriesForWholeRosterAndRange()
    {
        var rangeStart = new DateOnly(2026, 2, 1);
        var rangeEnd = new DateOnly(2026, 2, 7);

        await _sut.LoadRosterAvailabilityAsync(["player-a", "player-b"], GuildId, GuildBranchId, rangeStart, rangeEnd);

        _availabilityRepository.Verify(r => r.GetExceptionsOverlappingForUsersAsync(
            It.Is<IEnumerable<string>>(ids => ids.Contains("player-a") && ids.Contains("player-b")), rangeStart, rangeEnd, default), Times.Once);
        _availabilityRepository.Verify(r => r.GetPatternsForUsersAsync(
            It.Is<IEnumerable<string>>(ids => ids.Contains("player-a") && ids.Contains("player-b")), default), Times.Once);
    }

    [Fact]
    public async Task LoadRosterAvailabilityAsync_NonCollectionEnumerable_IsMaterializedBeforeQuerying()
    {
        // A plain generator (no ICollection<string>) forces the "as ICollection<string>" cast to
        // fail, exercising the "?? [.. playerDiscordIds]" materialization fallback.
        static IEnumerable<string> GeneratePlayerIds()
        {
            yield return "player-a";
            yield return "player-b";
        }

        var rangeStart = new DateOnly(2026, 2, 1);
        var rangeEnd = new DateOnly(2026, 2, 7);

        await _sut.LoadRosterAvailabilityAsync(GeneratePlayerIds(), GuildId, GuildBranchId, rangeStart, rangeEnd);

        _availabilityRepository.Verify(r => r.GetExceptionsOverlappingForUsersAsync(
            It.Is<IEnumerable<string>>(ids => ids.Contains("player-a") && ids.Contains("player-b")), rangeStart, rangeEnd, default), Times.Once);
    }

    [Fact]
    public async Task LoadRosterAvailabilityAsync_Lookup_FiltersDeclarationsPerPlayer()
    {
        var exceptionForA = new AvailabilityDeclaration { UserDiscordId = "player-a" };
        var exceptionForB = new AvailabilityDeclaration { UserDiscordId = "player-b" };
        _availabilityRepository.Setup(r => r.GetExceptionsOverlappingForUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), default))
            .ReturnsAsync([exceptionForA, exceptionForB]);

        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.Is<IReadOnlyCollection<AvailabilityDeclaration>>(ex => ex.Count == 1 && ex.Single().UserDiscordId == "player-a"),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Absent }]);
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.Is<IReadOnlyCollection<AvailabilityDeclaration>>(ex => ex.Count == 1 && ex.Single().UserDiscordId == "player-b"),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([new ResolvedDayAvailabilityResponse { Status = DayAvailabilityStatus.Available }]);

        var date = new DateOnly(2026, 2, 4);
        var lookup = await _sut.LoadRosterAvailabilityAsync(["player-a", "player-b"], GuildId, GuildBranchId, date, date);

        lookup.ResolveStatus("player-a", date).Should().Be(DayAvailabilityStatus.Absent);
        lookup.ResolveStatus("player-b", date).Should().Be(DayAvailabilityStatus.Available);
    }

    [Fact]
    public async Task LoadRosterAvailabilityAsync_Lookup_ResolveStatus_NoResolvedDay_DefaultsToAvailable()
    {
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([]);
        var date = new DateOnly(2026, 2, 4);

        var lookup = await _sut.LoadRosterAvailabilityAsync([PlayerDiscordId], GuildId, GuildBranchId, date, date);

        lookup.ResolveStatus(PlayerDiscordId, date).Should().Be(DayAvailabilityStatus.Available);
    }

    [Fact]
    public async Task LoadRosterAvailabilityAsync_Lookup_IsUnavailableAt_NoResolvedDay_ReturnsFalse()
    {
        _availabilityResolutionService.Setup(s => s.ResolveForScope(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<IReadOnlyCollection<AvailabilityDeclaration>>(),
                It.IsAny<IReadOnlyCollection<RecurringAvailabilityPattern>>(), GuildId, GuildBranchId))
            .Returns([]);
        var date = new DateOnly(2026, 2, 4);

        var lookup = await _sut.LoadRosterAvailabilityAsync([PlayerDiscordId], GuildId, GuildBranchId, date, date);

        lookup.IsUnavailableAt(PlayerDiscordId, date, new TimeOnly(20, 0)).Should().BeFalse();
    }
}
