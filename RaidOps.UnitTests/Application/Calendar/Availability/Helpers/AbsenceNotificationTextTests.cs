using FluentAssertions;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.Helpers;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Application.Calendar.Availability.Helpers;

public class AbsenceNotificationTextTests
{
    // ── DetermineKind ─────────────────────────────────────────────────────────

    [Fact]
    public void DetermineKind_NotPartial_ReturnsFullDay()
    {
        AbsenceNotificationText.DetermineKind(DayAvailabilityStatus.Absent, null, null).Should().Be(AbsenceKind.FullDay);
    }

    [Fact]
    public void DetermineKind_PartialWithOnlyFrom_ReturnsLateArrival()
    {
        AbsenceNotificationText.DetermineKind(DayAvailabilityStatus.Partial, new TimeOnly(21, 30), null).Should().Be(AbsenceKind.LateArrival);
    }

    [Fact]
    public void DetermineKind_PartialWithOnlyUntil_ReturnsEarlyLeave()
    {
        AbsenceNotificationText.DetermineKind(DayAvailabilityStatus.Partial, null, new TimeOnly(17, 0)).Should().Be(AbsenceKind.EarlyLeave);
    }

    [Fact]
    public void DetermineKind_PartialWithBothBounds_ReturnsPartialWindow()
    {
        AbsenceNotificationText.DetermineKind(DayAvailabilityStatus.Partial, new TimeOnly(9, 0), new TimeOnly(17, 0)).Should().Be(AbsenceKind.PartialWindow);
    }

    [Fact]
    public void DetermineKind_PartialWithNeitherBound_ReturnsPartialWindow()
    {
        AbsenceNotificationText.DetermineKind(DayAvailabilityStatus.Partial, null, null).Should().Be(AbsenceKind.PartialWindow);
    }

    // ── GetTitleAndColor ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("en", "New absence")]
    [InlineData("fr", "Nouvelle absence")]
    [InlineData("de", "Neue Abwesenheit")]
    public void GetTitleAndColor_SupportedLanguage_ReturnsLocalizedTitle(string language, string expectedTitle)
    {
        var (title, color) = AbsenceNotificationText.GetTitleAndColor(GuildNotificationEventType.AbsenceAdded, AbsenceKind.FullDay, language);

        title.Should().Be(expectedTitle);
        color.Should().Be(0xFEE75C);
    }

    [Fact]
    public void GetTitleAndColor_UnsupportedLanguage_FallsBackToEnglish()
    {
        var (title, _) = AbsenceNotificationText.GetTitleAndColor(GuildNotificationEventType.AbsenceRemoved, AbsenceKind.FullDay, "es");

        title.Should().Be("Absence removed");
    }

    // ── GetDescription ────────────────────────────────────────────────────────

    [Fact]
    public void GetDescription_SupportedLanguage_InterpolatesRequesterMention()
    {
        var description = AbsenceNotificationText.GetDescription(GuildNotificationEventType.AbsenceAdded, AbsenceKind.LateArrival, "fr", "12345");

        description.Should().Be("<@12345> a ajouté un retard.");
    }

    [Fact]
    public void GetDescription_UnsupportedLanguage_FallsBackToEnglish()
    {
        var description = AbsenceNotificationText.GetDescription(GuildNotificationEventType.AbsenceAdded, AbsenceKind.EarlyLeave, "es", "1");

        description.Should().Be("<@1> added an early leave.");
    }

    // ── FormatDateRange ───────────────────────────────────────────────────────

    [Fact]
    public void FormatDateRange_SameDay_ReturnsSingleDate()
    {
        var result = AbsenceNotificationText.FormatDateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), "en");

        result.Should().Be("7/1/2026");
    }

    [Fact]
    public void FormatDateRange_DifferentDays_ReturnsRangeWithArrow()
    {
        var result = AbsenceNotificationText.FormatDateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3), "fr");

        result.Should().Be("01/07/2026 → 03/07/2026");
    }

    [Fact]
    public void FormatDateRange_UnsupportedLanguage_FallsBackToEnUsCulture()
    {
        var result = AbsenceNotificationText.FormatDateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), "es");

        result.Should().Be("7/1/2026");
    }

    // ── FormatPartialSuffix ───────────────────────────────────────────────────

    [Theory]
    [InlineData("en", "from 21:30")]
    [InlineData("fr", "dès 21h30")]
    [InlineData("de", "ab 21:30")]
    public void FormatPartialSuffix_LateArrival_ReturnsFromPhrase(string language, string expected)
    {
        var result = AbsenceNotificationText.FormatPartialSuffix(AbsenceKind.LateArrival, new TimeOnly(21, 30), null, language);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "until 17:00")]
    [InlineData("fr", "jusqu'à 17h00")]
    [InlineData("de", "bis 17:00")]
    public void FormatPartialSuffix_EarlyLeave_ReturnsUntilPhrase(string language, string expected)
    {
        var result = AbsenceNotificationText.FormatPartialSuffix(AbsenceKind.EarlyLeave, null, new TimeOnly(17, 0), language);

        result.Should().Be(expected);
    }

    [Fact]
    public void FormatPartialSuffix_PartialWindow_ReturnsBothBoundsEnDash()
    {
        var result = AbsenceNotificationText.FormatPartialSuffix(AbsenceKind.PartialWindow, new TimeOnly(9, 0), new TimeOnly(17, 0), "en");

        result.Should().Be("09:00 – 17:00");
    }

    [Fact]
    public void FormatPartialSuffix_FullDay_ReturnsNull()
    {
        var result = AbsenceNotificationText.FormatPartialSuffix(AbsenceKind.FullDay, null, null, "en");

        result.Should().BeNull();
    }

    // ── GetPatternDescription ─────────────────────────────────────────────────

    [Fact]
    public void GetPatternDescription_WeeklyCycle_UsesWeekdayNamesAndNoCycleSuffix()
    {
        // Monday 2026-06-29 anchors offset 0.
        var anchor = new DateOnly(2026, 6, 29);
        var days = new List<PatternDayNotification>
        {
            new(1, DayAvailabilityStatus.Absent, "Nuit", null, null), // Tuesday
        };

        var result = AbsenceNotificationText.GetPatternDescription(
            GuildNotificationEventType.AbsenceAdded, "fr", "42", anchor, 7, days);

        result.Should().Be("<@42> a ajouté une nouvelle récurrence d'absences :\n\n- Mardi : Absent (Nuit)");
    }

    [Fact]
    public void GetPatternDescription_NonWeeklyCycle_AppendsCycleLengthSuffixAndUsesDayNumbers()
    {
        var anchor = new DateOnly(2026, 6, 29);
        var days = new List<PatternDayNotification>
        {
            new(2, DayAvailabilityStatus.Absent, null, null, null),
        };

        var result = AbsenceNotificationText.GetPatternDescription(
            GuildNotificationEventType.AbsenceAdded, "en", "42", anchor, 4, days);

        result.Should().Be("<@42> added a new recurring absence pattern: (cycle of 4 days)\n\n- Day 3 : Absent");
    }

    [Theory]
    [InlineData("fr", "(cycle de 5 jours)")]
    [InlineData("de", "(Zyklus von 5 Tagen)")]
    [InlineData("en", "(cycle of 5 days)")]
    public void GetPatternDescription_NonWeeklyCycle_LocalizesCycleLengthSuffix(string language, string expectedSuffix)
    {
        var days = new List<PatternDayNotification> { new(0, DayAvailabilityStatus.Absent, null, null, null) };

        var result = AbsenceNotificationText.GetPatternDescription(
            GuildNotificationEventType.AbsenceAdded, language, "1", new DateOnly(2026, 6, 29), 5, days);

        result.Should().Contain(expectedSuffix);
    }

    [Fact]
    public void GetPatternDescription_OrdersDaysByOffset()
    {
        var days = new List<PatternDayNotification>
        {
            new(3, DayAvailabilityStatus.Absent, null, null, null),
            new(0, DayAvailabilityStatus.Absent, null, null, null),
        };

        var result = AbsenceNotificationText.GetPatternDescription(
            GuildNotificationEventType.AbsenceAdded, "en", "1", new DateOnly(2026, 6, 29), 4, days);

        var firstDayIndex = result.IndexOf("Day 1", StringComparison.Ordinal);
        var secondDayIndex = result.IndexOf("Day 4", StringComparison.Ordinal);
        firstDayIndex.Should().BeGreaterThan(0);
        secondDayIndex.Should().BeGreaterThan(firstDayIndex);
    }

    [Fact]
    public void GetPatternDescription_PartialDayWithoutReason_UsesTimeSuffixOnly()
    {
        var days = new List<PatternDayNotification>
        {
            new(4, DayAvailabilityStatus.Partial, null, new TimeOnly(18, 0), new TimeOnly(22, 0)),
        };

        var result = AbsenceNotificationText.GetPatternDescription(
            GuildNotificationEventType.AbsenceAdded, "en", "1", new DateOnly(2026, 6, 29), 7, days);

        result.Should().Contain("- Friday : 18:00 – 22:00");
        result.Should().NotContain("(");
    }

    [Fact]
    public void GetPatternDescription_GermanFullDay_UsesAbwesendWord()
    {
        var days = new List<PatternDayNotification> { new(0, DayAvailabilityStatus.Absent, null, null, null) };

        var result = AbsenceNotificationText.GetPatternDescription(
            GuildNotificationEventType.AbsenceAdded, "de", "1", new DateOnly(2026, 6, 29), 7, days);

        result.Should().Contain("Abwesend");
    }

    [Fact]
    public void GetPatternDescription_UnsupportedLanguage_FallsBackToEnglishTemplate()
    {
        var days = new List<PatternDayNotification> { new(0, DayAvailabilityStatus.Absent, null, null, null) };

        var result = AbsenceNotificationText.GetPatternDescription(
            GuildNotificationEventType.AbsenceRemoved, "es", "1", new DateOnly(2026, 6, 29), 7, days);

        result.Should().StartWith("<@1> removed a recurring absence pattern:");
    }
}
