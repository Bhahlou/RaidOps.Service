using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <summary>
/// Default <see cref="IRaidLockoutService"/> implementation.
/// </summary>
public class RaidLockoutService : IRaidLockoutService
{
    /// <inheritdoc/>
    public DateTime GetLockoutWindowStart(DateTime baselineAnchorUtc, int baselineCadenceDays, IReadOnlyCollection<RaidLockoutCadenceOverride> overrides, DateTime utcInstant)
    {
        var cursor = baselineAnchorUtc;
        if (utcInstant <= cursor)
            return cursor;

        // Breakpoints are every instant at which the applicable cadence *might* change (an override
        // starting or ending). They don't need to be exact regime boundaries for `cursor` — an
        // imprecise breakpoint just costs one harmless extra loop iteration, since the cadence
        // lookup is re-evaluated at every jump. Anchored to the baseline's time-of-day so a
        // day-granularity override boundary still lines up with the actual reset hour.
        var breakpoints = BuildBreakpoints(overrides, baselineAnchorUtc.TimeOfDay);

        // Bounded by breakpoints.Count + 1 crossings — never a day-by-day scan.
        while (true)
        {
            var cadence = Math.Max(1, CadenceAt(baselineCadenceDays, overrides, cursor));

            // Deliberately not `breakpoints.Cast<DateTime?>().FirstOrDefault(b => b > cursor)`:
            // that lifts `>` to a nullable comparison whose "operand is null" branch can never
            // execute (every cast element is a real DateTime), which coverage tooling flags as a
            // permanently half-covered line. Plain non-nullable comparison here, nullable only at
            // the boundary where "no breakpoint found" actually needs representing.
            var laterBreakpoints = breakpoints.Where(b => b > cursor).ToList();
            var nextBreakpoint = laterBreakpoints.Count > 0 ? laterBreakpoints[0] : (DateTime?)null;
            var ceiling = nextBreakpoint.HasValue && nextBreakpoint.Value < utcInstant ? nextBreakpoint.Value : utcInstant;

            cursor = Advance(cursor, ceiling, cadence);

            var nextReset = cursor.AddDays(cadence);
            if (nextReset > utcInstant)
                return cursor;

            // The next reset already happened on or before `utcInstant` — cross into it and keep
            // going, re-evaluating cadence from that point (it may now fall in a different regime).
            cursor = nextReset;
        }
    }

    /// <summary>Jumps <paramref name="cursor"/> forward by the largest whole multiple of <paramref name="cadence"/> that stays within <paramref name="ceiling"/>.</summary>
    private static DateTime Advance(DateTime cursor, DateTime ceiling, int cadence)
    {
        var daysAvailable = (ceiling - cursor).TotalDays;
        if (daysAvailable <= 0)
            return cursor;

        var steps = (long)(daysAvailable / cadence);
        return cursor.AddDays(steps * cadence);
    }

    /// <summary>Returns the cadence (in days) governing a step starting at <paramref name="cursor"/>: the active override's, or the resolved baseline.</summary>
    private static int CadenceAt(int baselineCadenceDays, IReadOnlyCollection<RaidLockoutCadenceOverride> overrides, DateTime cursor)
    {
        var cursorDate = DateOnly.FromDateTime(cursor);
        var active = overrides.FirstOrDefault(o => o.EffectiveFrom <= cursorDate && (!o.EffectiveUntil.HasValue || o.EffectiveUntil.Value >= cursorDate));
        return active?.CadenceDays ?? baselineCadenceDays;
    }

    private static List<DateTime> BuildBreakpoints(IReadOnlyCollection<RaidLockoutCadenceOverride> overrides, TimeSpan resetTimeOfDay)
    {
        var breakpoints = new List<DateTime>();
        foreach (var o in overrides)
        {
            breakpoints.Add(ToUtcBreakpoint(o.EffectiveFrom, resetTimeOfDay));
            if (o.EffectiveUntil.HasValue)
                breakpoints.Add(ToUtcBreakpoint(o.EffectiveUntil.Value.AddDays(1), resetTimeOfDay));
        }

        breakpoints.Sort();
        return breakpoints;
    }

    /// <summary>Combines a calendar day with the baseline's reset time-of-day to get a UTC breakpoint instant.</summary>
    private static DateTime ToUtcBreakpoint(DateOnly date, TimeSpan resetTimeOfDay)
        => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).Add(resetTimeOfDay), DateTimeKind.Utc);
}
