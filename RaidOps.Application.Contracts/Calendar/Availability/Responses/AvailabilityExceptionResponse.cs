using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Calendar.Availability.Responses;

/// <summary>
/// DTO representing a one-off availability exception, as returned for editing.
/// </summary>
public class AvailabilityExceptionResponse
{
    /// <summary>The exception's identifier.</summary>
    public int Id { get; set; }

    /// <summary>First date covered by this exception (inclusive).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last date covered by this exception (inclusive).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Declared status for every date in the range.</summary>
    public DayAvailabilityStatus Status { get; set; }

    /// <summary>Free-text reason, if any.</summary>
    public string? Reason { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available.</summary>
    public TimeOnly? AvailableUntil { get; set; }
}
