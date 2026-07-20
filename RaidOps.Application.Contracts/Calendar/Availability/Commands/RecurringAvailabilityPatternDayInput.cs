using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// One day of a recurring pattern's cycle that is not fully available, as submitted when
/// creating or replacing a <see cref="CreateRecurringAvailabilityPatternCommand"/>'s day set.
/// </summary>
public class RecurringAvailabilityPatternDayInput
{
    /// <summary>Zero-based offset within the pattern's cycle.</summary>
    public required int OffsetInCycle { get; set; }

    /// <summary>Declared status for this offset. Only <see cref="DayAvailabilityStatus.Absent"/> or <see cref="DayAvailabilityStatus.Partial"/> are meaningful here.</summary>
    public required DayAvailabilityStatus Status { get; set; }

    /// <summary>Optional free-text reason.</summary>
    public string? Reason { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available.</summary>
    public TimeOnly? AvailableUntil { get; set; }
}
