namespace RaidOps.Domain.Enums;

/// <summary>
/// A member's declared availability for a single calendar day.
/// </summary>
public enum DayAvailabilityStatus
{
    /// <summary>
    /// Explicitly available. Only meaningful on a one-off <c>AvailabilityDeclaration</c>, used to
    /// override a recurring pattern for a specific date (e.g. "exceptionally present this Wednesday").
    /// Recurring pattern days never use this value — the absence of a day row already means available.
    /// </summary>
    Available = 0,

    /// <summary>Not available at all for the day.</summary>
    Absent = 1,

    /// <summary>
    /// Available for part of the day only, bounded by <c>AvailableFrom</c> and/or <c>AvailableUntil</c>
    /// (e.g. arriving late, or leaving early for a night shift).
    /// </summary>
    Partial = 2,
}
