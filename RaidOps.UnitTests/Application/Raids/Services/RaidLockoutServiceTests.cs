using FluentAssertions;
using RaidOps.Application.Implementations.Raids.Services;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.UnitTests.Application.Raids.Services;

public class RaidLockoutServiceTests
{
    private readonly RaidLockoutService _sut = new();

    private static readonly DateTime Anchor = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc); // Thursday

    [Fact]
    public void GetLockoutWindowStart_InstantBeforeAnchor_ReturnsAnchor()
    {
        var result = _sut.GetLockoutWindowStart(Anchor, 7, [], Anchor.AddDays(-3));

        result.Should().Be(Anchor);
    }

    [Fact]
    public void GetLockoutWindowStart_InstantEqualToAnchor_ReturnsAnchor()
    {
        var result = _sut.GetLockoutWindowStart(Anchor, 7, [], Anchor);

        result.Should().Be(Anchor);
    }

    [Fact]
    public void GetLockoutWindowStart_InstantWithinFirstWindow_ReturnsAnchor()
    {
        var result = _sut.GetLockoutWindowStart(Anchor, 7, [], Anchor.AddDays(3));

        result.Should().Be(Anchor);
    }

    [Fact]
    public void GetLockoutWindowStart_InstantExactlyOnNextReset_ReturnsThatReset()
    {
        // A reset instant is the start of the *next* window, not the end of the current one.
        var result = _sut.GetLockoutWindowStart(Anchor, 7, [], Anchor.AddDays(7));

        result.Should().Be(Anchor.AddDays(7));
    }

    [Fact]
    public void GetLockoutWindowStart_ManyCadencesLater_JumpsDirectlyWithoutDayByDayScan()
    {
        // 30 days after a weekly anchor: resets at +0,7,14,21,28,35 — window covering +30 starts at +28.
        var result = _sut.GetLockoutWindowStart(Anchor, 7, [], Anchor.AddDays(30));

        result.Should().Be(Anchor.AddDays(28));
    }

    [Fact]
    public void GetLockoutWindowStart_ZeroCadence_ClampsToOneDay()
    {
        var result = _sut.GetLockoutWindowStart(Anchor, 0, [], Anchor.AddDays(5));

        result.Should().Be(Anchor.AddDays(5));
    }

    [Fact]
    public void GetLockoutWindowStart_OverrideBeforeItsEffectiveDate_UsesBaselineCadence()
    {
        var overrides = new List<RaidLockoutCadenceOverride>
        {
            new()
            {
                CadenceDays = 2,
                EffectiveFrom = DateOnly.FromDateTime(Anchor.AddDays(100)),
                EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(110)),
            },
        };

        var result = _sut.GetLockoutWindowStart(Anchor, 7, overrides, Anchor.AddDays(3));

        result.Should().Be(Anchor);
    }

    [Fact]
    public void GetLockoutWindowStart_AllBreakpointsBeforeCursor_TreatsThemAsAbsentForThisStep()
    {
        // The override's whole date range is already in the past relative to the anchor itself, so
        // both of its breakpoints sit before the loop's very first cursor position (the anchor) —
        // distinct from having no overrides at all: the breakpoints list is non-empty here, but
        // none of them qualify as the "next" one from the cursor's viewpoint.
        var overrides = new List<RaidLockoutCadenceOverride>
        {
            new()
            {
                CadenceDays = 2,
                EffectiveFrom = DateOnly.FromDateTime(Anchor.AddDays(-10)),
                EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(-5)),
            },
        };

        var result = _sut.GetLockoutWindowStart(Anchor, 7, overrides, Anchor.AddDays(10));

        result.Should().Be(Anchor.AddDays(7));
    }

    [Fact]
    public void GetLockoutWindowStart_ActiveOverride_UsesOverrideCadenceInsteadOfBaseline()
    {
        // Override drops cadence from 7 to 2 for the anchor's whole first baseline cycle.
        var overrides = new List<RaidLockoutCadenceOverride>
        {
            new()
            {
                CadenceDays = 2,
                EffectiveFrom = DateOnly.FromDateTime(Anchor),
                EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(9)),
            },
        };

        // Resets under the override land on +0, +2, +4, +6, +8 — instant at +5 sits in the +4 window.
        var result = _sut.GetLockoutWindowStart(Anchor, 7, overrides, Anchor.AddDays(5));

        result.Should().Be(Anchor.AddDays(4));
    }

    [Fact]
    public void GetLockoutWindowStart_InstantExactlyOnOverrideReset_ReturnsThatReset()
    {
        var overrides = new List<RaidLockoutCadenceOverride>
        {
            new()
            {
                CadenceDays = 2,
                EffectiveFrom = DateOnly.FromDateTime(Anchor),
                EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(9)),
            },
        };

        var result = _sut.GetLockoutWindowStart(Anchor, 7, overrides, Anchor.AddDays(4));

        result.Should().Be(Anchor.AddDays(4));
    }

    [Fact]
    public void GetLockoutWindowStart_AfterOverrideExpires_ResumesFromLastComputedCursorNotOriginalAnchor()
    {
        // Baseline cadence 10, override cadence 2 for days [0..9]. After the override lapses
        // (day 10), cadence resumes at 10 days — but re-anchored from wherever the cursor landed
        // while crossing the override/baseline boundary, not from the original anchor. Traced by
        // hand against RaidLockoutService's own documented "re-evaluate cadence at every jump" rule:
        // the override regime jumps the cursor to +10 (5 * 2-day steps), then crosses into +12
        // (nextReset = +10 + 2, still an override-cadence reset since the loop crosses BEFORE
        // re-checking the regime), and from +12 (now baseline territory) the 10-day cadence keeps
        // the window open through +15.
        var overrides = new List<RaidLockoutCadenceOverride>
        {
            new()
            {
                CadenceDays = 2,
                EffectiveFrom = DateOnly.FromDateTime(Anchor),
                EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(9)),
            },
        };

        var result = _sut.GetLockoutWindowStart(Anchor, 10, overrides, Anchor.AddDays(15));

        result.Should().Be(Anchor.AddDays(12));
    }

    [Fact]
    public void GetLockoutWindowStart_OpenEndedOverride_HasNoUntilBreakpointAndAppliesIndefinitely()
    {
        var overrides = new List<RaidLockoutCadenceOverride>
        {
            new()
            {
                CadenceDays = 3,
                EffectiveFrom = DateOnly.FromDateTime(Anchor),
                EffectiveUntil = null,
            },
        };

        // Resets every 3 days forever: +0, +3, +6, +9, +12 — instant at +100 falls in the +99 window.
        var result = _sut.GetLockoutWindowStart(Anchor, 7, overrides, Anchor.AddDays(100));

        result.Should().Be(Anchor.AddDays(99));
    }

    [Fact]
    public void GetLockoutWindowStart_UnsortedOverrides_StillProducesCorrectBreakpointOrder()
    {
        var laterOverride = new RaidLockoutCadenceOverride
        {
            CadenceDays = 4,
            EffectiveFrom = DateOnly.FromDateTime(Anchor.AddDays(20)),
            EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(30)),
        };
        var earlierOverride = new RaidLockoutCadenceOverride
        {
            CadenceDays = 2,
            EffectiveFrom = DateOnly.FromDateTime(Anchor),
            EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(9)),
        };

        // Passed out of chronological order — BuildBreakpoints sorts internally.
        var overrides = new List<RaidLockoutCadenceOverride> { laterOverride, earlierOverride };

        var result = _sut.GetLockoutWindowStart(Anchor, 7, overrides, Anchor.AddDays(5));

        result.Should().Be(Anchor.AddDays(4));
    }

    [Fact]
    public void GetLockoutWindowStart_BreakpointStopsCursorExactlyOnInstant_SecondPassIsAZeroWidthAdvance()
    {
        // A single-day breakpoint (from an override that's never actually active at any evaluated
        // cursor) interrupts the first jump one day early, so the unconstrained nextReset from
        // there lands exactly on the instant. The loop then re-enters with cursor == ceiling == the
        // instant, exercising Advance's own "nothing to jump" guard rather than reaching the same
        // result purely through the single-shot jump (see the "InstantExactlyOnNextReset" case above).
        var overrides = new List<RaidLockoutCadenceOverride>
        {
            new()
            {
                CadenceDays = 99, // never active at day 0 or day 3 — only its date range creates a breakpoint.
                EffectiveFrom = DateOnly.FromDateTime(Anchor.AddDays(2)),
                EffectiveUntil = DateOnly.FromDateTime(Anchor.AddDays(2)),
            },
        };

        var result = _sut.GetLockoutWindowStart(Anchor, 3, overrides, Anchor.AddDays(3));

        result.Should().Be(Anchor.AddDays(3));
    }
}
