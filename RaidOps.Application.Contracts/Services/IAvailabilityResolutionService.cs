using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Domain.Models.Calendar;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Resolves a member's day-by-day availability over a date range from their one-off exceptions
/// and recurring patterns. Pure function of its inputs — no persistence, easily unit-testable.
/// </summary>
public interface IAvailabilityResolutionService
{
    /// <summary>
    /// Returns the resolved status for every date in <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>.
    /// For each date, a matching <paramref name="exceptions"/> entry always takes precedence over
    /// <paramref name="patterns"/>; when several active patterns match the same date, the most
    /// restrictive status wins (<c>Absent</c> &gt; <c>Partial</c> &gt; <c>Available</c>).
    /// </summary>
    /// <param name="rangeStart">First date to resolve (inclusive).</param>
    /// <param name="rangeEnd">Last date to resolve (inclusive).</param>
    /// <param name="exceptions">The member's one-off exceptions, expected to overlap the range.</param>
    /// <param name="patterns">The member's recurring patterns (active or not — inactive ones are ignored).</param>
    List<ResolvedDayAvailabilityResponse> Resolve(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        IReadOnlyCollection<AvailabilityDeclaration> exceptions,
        IReadOnlyCollection<RecurringAvailabilityPattern> patterns);
}
