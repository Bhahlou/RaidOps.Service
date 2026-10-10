using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Calendar.Availability.Responses;

/// <summary>
/// The resolved availability status for a single date, after applying any one-off exception on
/// top of the active recurring patterns.
/// </summary>
public class ResolvedDayAvailabilityResponse
{
    /// <summary>The resolved date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The resolved status for this date.</summary>
    public DayAvailabilityStatus Status { get; set; }

    /// <summary>Free-text reason backing this status, if any.</summary>
    public string? Reason { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available.</summary>
    public TimeOnly? AvailableUntil { get; set; }

    /// <summary>Whether this date is covered by a one-off exception rather than a recurring pattern.</summary>
    public bool IsException { get; set; }
}
