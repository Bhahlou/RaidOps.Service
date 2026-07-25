using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <summary>
/// Default <see cref="IRaidLockoutService"/> implementation.
/// </summary>
public class RaidLockoutService : IRaidLockoutService
{
    /// <inheritdoc/>
    public DateOnly GetLockoutWindowStart(DateOnly baselineAnchorDate, int baselineCadenceDays, IReadOnlyCollection<RaidLockoutCadenceOverride> overrides, DateOnly date)
    {
        var cursor = baselineAnchorDate;
        if (date <= cursor)
            return cursor;

        // Breakpoints are every date at which the applicable cadence *might* change (an override
        // starting or ending). They don't need to be exact regime boundaries for `cursor` — an
        // imprecise breakpoint just costs one harmless extra loop iteration, since the cadence
        // lookup is re-evaluated at every jump.
        var breakpoints = BuildBreakpoints(overrides);

        // Bounded by breakpoints.Count + 1 crossings — never a day-by-day scan.
        while (true)
        {
            var cadence = Math.Max(1, CadenceAt(baselineCadenceDays, overrides, cursor));
            var nextBreakpoint = breakpoints.Cast<DateOnly?>().FirstOrDefault(b => b > cursor);
            var ceiling = nextBreakpoint.HasValue && nextBreakpoint.Value < date ? nextBreakpoint.Value : date;

            cursor = Advance(cursor, ceiling, cadence);

            var nextReset = cursor.AddDays(cadence);
            if (nextReset > date)
                return cursor;

            // The next reset already happened on or before `date` — cross into it and keep going,
            // re-evaluating cadence from that point (it may now fall in a different regime).
            cursor = nextReset;
        }
    }

    /// <summary>Jumps <paramref name="cursor"/> forward by the largest whole multiple of <paramref name="cadence"/> that stays within <paramref name="ceiling"/>.</summary>
    private static DateOnly Advance(DateOnly cursor, DateOnly ceiling, int cadence)
    {
        var daysAvailable = ceiling.DayNumber - cursor.DayNumber;
        if (daysAvailable <= 0)
            return cursor;

        var steps = daysAvailable / cadence;
        return cursor.AddDays(steps * cadence);
    }

    /// <summary>Returns the cadence (in days) governing a step starting at <paramref name="cursor"/>: the active override's, or the resolved baseline.</summary>
    private static int CadenceAt(int baselineCadenceDays, IReadOnlyCollection<RaidLockoutCadenceOverride> overrides, DateOnly cursor)
    {
        var active = overrides.FirstOrDefault(o => o.EffectiveFrom <= cursor && (!o.EffectiveUntil.HasValue || o.EffectiveUntil.Value >= cursor));
        return active?.CadenceDays ?? baselineCadenceDays;
    }

    private static List<DateOnly> BuildBreakpoints(IReadOnlyCollection<RaidLockoutCadenceOverride> overrides)
    {
        var breakpoints = new List<DateOnly>();
        foreach (var o in overrides)
        {
            breakpoints.Add(o.EffectiveFrom);
            if (o.EffectiveUntil.HasValue)
                breakpoints.Add(o.EffectiveUntil.Value.AddDays(1));
        }

        breakpoints.Sort();
        return breakpoints;
    }
}
