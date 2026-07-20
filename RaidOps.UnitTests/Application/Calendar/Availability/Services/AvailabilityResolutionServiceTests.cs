using FluentAssertions;
using RaidOps.Application.Implementations.Calendar.Availability.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;

namespace RaidOps.UnitTests.Application.Calendar.Availability.Services;

public class AvailabilityResolutionServiceTests
{
    private readonly AvailabilityResolutionService _sut = new();

    [Fact]
    public void Resolve_NoExceptionsNoPatterns_AllDatesResolveAvailable()
    {
        var rangeStart = new DateOnly(2026, 1, 1);
        var rangeEnd = new DateOnly(2026, 1, 5);

        var result = _sut.Resolve(rangeStart, rangeEnd, [], []);

        result.Should().HaveCount(5);
        result.Should().OnlyContain(d => d.Status == DayAvailabilityStatus.Available && !d.IsException);
    }

    [Fact]
    public void Resolve_ExceptionCoversDate_TakesPrecedenceOverMatchingPattern()
    {
        var date = new DateOnly(2026, 1, 5);
        var exception = new AvailabilityDeclaration
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            StartDate = date, EndDate = date,
            Status = DayAvailabilityStatus.Partial, Reason = "Doctor appointment",
            AvailableFrom = new TimeOnly(9, 0), AvailableUntil = new TimeOnly(12, 0),
        };
        var pattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2026, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }],
        };

        var result = _sut.Resolve(date, date, [exception], [pattern]);

        result.Should().ContainSingle();
        var resolved = result[0];
        resolved.Status.Should().Be(DayAvailabilityStatus.Partial);
        resolved.Reason.Should().Be("Doctor appointment");
        resolved.AvailableFrom.Should().Be(new TimeOnly(9, 0));
        resolved.AvailableUntil.Should().Be(new TimeOnly(12, 0));
        resolved.IsException.Should().BeTrue();
    }

    [Fact]
    public void Resolve_WeeklyPattern_MatchesTargetWeekdayAcrossMultipleWeeks()
    {
        var anchor = new DateOnly(2026, 1, 12); // Monday
        var pattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = anchor, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 2, Status = DayAvailabilityStatus.Absent }], // Wednesday
        };

        var beforeWednesday = new DateOnly(2026, 1, 7);   // week before anchor's week
        var anchorWeekWednesday = new DateOnly(2026, 1, 14);
        var laterWednesday = new DateOnly(2026, 2, 4);     // several weeks after
        var anchorMonday = anchor;

        var result = _sut.Resolve(beforeWednesday, laterWednesday, [], [pattern]);

        result.First(d => d.Date == beforeWednesday).Status.Should().Be(DayAvailabilityStatus.Absent);
        result.First(d => d.Date == anchorWeekWednesday).Status.Should().Be(DayAvailabilityStatus.Absent);
        result.First(d => d.Date == laterWednesday).Status.Should().Be(DayAvailabilityStatus.Absent);
        result.First(d => d.Date == anchorMonday).Status.Should().Be(DayAvailabilityStatus.Available);
    }

    [Fact]
    public void Resolve_AnchorDateAfterQueriedDate_NegativeModuloStillResolvesCorrectOffset()
    {
        var anchor = new DateOnly(2026, 1, 12); // Monday
        var oneWeekBeforeAnchor = new DateOnly(2026, 1, 5); // also a Monday, offset 0
        var pattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = anchor, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }],
        };

        var result = _sut.Resolve(oneWeekBeforeAnchor, oneWeekBeforeAnchor, [], [pattern]);

        result.Should().ContainSingle();
        result[0].Status.Should().Be(DayAvailabilityStatus.Absent);
    }

    [Fact]
    public void Resolve_TwoPatternsMatchSameDateDifferentStatus_MostRestrictiveWinsRegardlessOfOrder()
    {
        var date = new DateOnly(2026, 1, 12);
        var partialPattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Partial }],
        };
        var absentPattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }],
        };

        var resultAbsentFirst = _sut.Resolve(date, date, [], [absentPattern, partialPattern]);
        var resultPartialFirst = _sut.Resolve(date, date, [], [partialPattern, absentPattern]);

        resultAbsentFirst[0].Status.Should().Be(DayAvailabilityStatus.Absent);
        resultPartialFirst[0].Status.Should().Be(DayAvailabilityStatus.Absent);
    }

    [Fact]
    public void Resolve_OnePatternHasNoDayAtOffsetAndAnotherHasPartial_ResolvesPartialNotAvailable()
    {
        var date = new DateOnly(2026, 1, 12);
        var patternWithoutDay = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [], // sparse — no row for offset 0
        };
        var patternWithPartial = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Partial }],
        };

        var result = _sut.Resolve(date, date, [], [patternWithoutDay, patternWithPartial]);

        result[0].Status.Should().Be(DayAvailabilityStatus.Partial);
    }

    [Fact]
    public void Resolve_PatternEffectiveFromInFuture_IsSkippedAndFallsBackToAvailable()
    {
        var date = new DateOnly(2026, 1, 12);
        var futurePattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = date.AddDays(1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }],
        };

        var result = _sut.Resolve(date, date, [], [futurePattern]);

        result[0].Status.Should().Be(DayAvailabilityStatus.Available);
    }

    [Fact]
    public void Resolve_ClosedPatternVersion_AppliesOnlyUpToEffectiveUntil_NewVersionAppliesAfter()
    {
        const int offset = 0;
        var anchor = new DateOnly(2026, 1, 1);
        var oldVersion = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = anchor,
            EffectiveFrom = new DateOnly(2026, 1, 1), EffectiveUntil = new DateOnly(2026, 1, 19),
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = offset, Status = DayAvailabilityStatus.Absent }],
        };
        var newVersion = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = anchor,
            EffectiveFrom = new DateOnly(2026, 1, 20), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = offset, Status = DayAvailabilityStatus.Partial }],
        };
        var dateUnderOldVersion = new DateOnly(2026, 1, 15);
        var dateUnderNewVersion = new DateOnly(2026, 1, 22); // same cycle offset as dateUnderOldVersion, but after the new version's EffectiveFrom

        var resultOld = _sut.Resolve(dateUnderOldVersion, dateUnderOldVersion, [], [oldVersion, newVersion]);
        var resultNew = _sut.Resolve(dateUnderNewVersion, dateUnderNewVersion, [], [oldVersion, newVersion]);

        resultOld[0].Status.Should().Be(DayAvailabilityStatus.Absent);
        resultNew[0].Status.Should().Be(DayAvailabilityStatus.Partial);
    }

    [Fact]
    public void Resolve_DateRangeMatchesButNoDayRowAtComputedOffset_FallsThroughToAvailable()
    {
        var date = new DateOnly(2026, 1, 12);
        var sparsePattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 1, Status = DayAvailabilityStatus.Absent }], // different offset
        };

        var result = _sut.Resolve(date, date, [], [sparsePattern]);

        result[0].Status.Should().Be(DayAvailabilityStatus.Available);
    }

    [Fact]
    public void Resolve_ExplicitAvailableDayCompetesWithAbsentPattern_AbsentStillWins()
    {
        // RecurringAvailabilityPatternDay.Status is only ever meant to hold Absent/Partial in
        // practice (sparse storage — no row already means Available), but the enum technically
        // allows an explicit Available row too. Restrictiveness() must still rank it as the least
        // restrictive (its `_ => 0` default arm) so a second pattern's Absent/Partial always wins.
        var date = new DateOnly(2026, 1, 12);
        var explicitAvailablePattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Available }],
        };
        var absentPattern = new RecurringAvailabilityPattern
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            CycleLengthDays = 7, AnchorDate = date, EffectiveFrom = new DateOnly(2025, 1, 1), EffectiveUntil = null,
            Days = [new RecurringAvailabilityPatternDay { OffsetInCycle = 0, Status = DayAvailabilityStatus.Absent }],
        };

        var result = _sut.Resolve(date, date, [], [explicitAvailablePattern, absentPattern]);

        result[0].Status.Should().Be(DayAvailabilityStatus.Absent);
    }

    [Fact]
    public void Resolve_MultiDayRange_ReturnsOneEntryPerDateInOrderInclusiveOfBothEnds()
    {
        var rangeStart = new DateOnly(2026, 1, 1);
        var rangeEnd = new DateOnly(2026, 1, 4);

        var result = _sut.Resolve(rangeStart, rangeEnd, [], []);

        result.Select(d => d.Date).Should().Equal(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4));
    }

    [Fact]
    public void Resolve_RangeSpansBeforeDuringAndAfterAnException_OnlyMatchesDaysWithinItsBounds()
    {
        // A non-empty exceptions list where most queried dates DON'T match it — exercises both
        // halves of the `date >= StartDate && date <= EndDate` predicate independently (day 1 fails
        // the upper bound, day 3 fails neither, day 5 fails the lower bound never even applying —
        // day 4 fails the lower bound while the upper bound would hold), not just the all-empty or
        // all-matching cases the other tests already cover.
        var exception = new AvailabilityDeclaration
        {
            UserDiscordId = "user-1", GuildId = "guild-1",
            StartDate = new DateOnly(2026, 1, 3), EndDate = new DateOnly(2026, 1, 3),
            Status = DayAvailabilityStatus.Absent,
        };

        var result = _sut.Resolve(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), [exception], []);

        result[0].Status.Should().Be(DayAvailabilityStatus.Available); // Jan 1 — before the exception
        result[1].Status.Should().Be(DayAvailabilityStatus.Available); // Jan 2 — still before
        result[2].Status.Should().Be(DayAvailabilityStatus.Absent);    // Jan 3 — the exception itself
        result[3].Status.Should().Be(DayAvailabilityStatus.Available); // Jan 4 — after
        result[4].Status.Should().Be(DayAvailabilityStatus.Available); // Jan 5 — still after
    }
}
