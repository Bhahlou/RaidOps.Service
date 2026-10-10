namespace RaidOps.Domain.Enums;

/// <summary>
/// The shape of an "Absences" family Discord notification's underlying day — derived from
/// <see cref="DayAvailabilityStatus"/> plus, for <see cref="DayAvailabilityStatus.Partial"/>,
/// which of <c>AvailableFrom</c>/<c>AvailableUntil</c> is set. Distinct wording per kind is what
/// tells an officer "this is a full-day absence" apart from "this member is just running late" at
/// a glance, instead of both reading as a generic "absence".
/// </summary>
public enum AbsenceKind
{
    /// <summary><see cref="DayAvailabilityStatus.Absent"/> — unavailable for the whole day.</summary>
    FullDay,

    /// <summary>Partial with only <c>AvailableFrom</c> set — unavailable until that time, then available.</summary>
    LateArrival,

    /// <summary>Partial with only <c>AvailableUntil</c> set — available until that time, then unavailable.</summary>
    EarlyLeave,

    /// <summary>Partial with both bounds set — available only within that window.</summary>
    PartialWindow,

    /// <summary>A recurring pattern's whole day set (multiple days, possibly mixing Absent and Partial), announced as one notification.</summary>
    RecurringPattern,
}
