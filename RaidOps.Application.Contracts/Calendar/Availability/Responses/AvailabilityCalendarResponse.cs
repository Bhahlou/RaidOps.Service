namespace RaidOps.Application.Contracts.Calendar.Availability.Responses;

/// <summary>
/// DTO returned by <see cref="Queries.GetMyAvailabilityQuery"/> — the resolved calendar for the
/// requested range, plus the raw exceptions and patterns backing it.
/// </summary>
public class AvailabilityCalendarResponse
{
    /// <summary>The resolved status for every date in the requested range.</summary>
    public List<ResolvedDayAvailabilityResponse> Days { get; set; } = [];

    /// <summary>One-off exceptions overlapping the requested range.</summary>
    public List<AvailabilityExceptionResponse> Exceptions { get; set; } = [];

    /// <summary>All of the member's recurring patterns for this guild (active or not).</summary>
    public List<RecurringAvailabilityPatternResponse> Patterns { get; set; } = [];
}
