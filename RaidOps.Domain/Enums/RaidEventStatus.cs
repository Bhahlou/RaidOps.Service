namespace RaidOps.Domain.Enums;

/// <summary>
/// Lifecycle status of a <c>RaidEvent</c>.
/// </summary>
public enum RaidEventStatus
{
    /// <summary>Upcoming or in-progress — counts toward lockout consumption and roster planning.</summary>
    Scheduled = 0,

    /// <summary>Already happened. Still counts toward lockout consumption (history).</summary>
    Completed = 1,
}
