using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Calendar.Availability.Responses;

/// <summary>
/// DTO representing a single day of a recurring pattern's cycle that is not fully available.
/// </summary>
public class RecurringAvailabilityPatternDayResponse
{
    /// <summary>Zero-based offset within the pattern's cycle.</summary>
    public int OffsetInCycle { get; set; }

    /// <summary>Declared status for this offset.</summary>
    public DayAvailabilityStatus Status { get; set; }

    /// <summary>Free-text reason, if any.</summary>
    public string? Reason { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available.</summary>
    public TimeOnly? AvailableUntil { get; set; }
}
