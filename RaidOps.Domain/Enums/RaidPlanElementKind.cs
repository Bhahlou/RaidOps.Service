namespace RaidOps.Domain.Enums;

/// <summary>
/// What a <see cref="Models.Raids.Plans.RaidPlanElement"/> renders as on the strategy-board canvas.
/// Drives which of its field groups are populated — same "one table, active fields depend on Kind"
/// convention as <see cref="AttributionCellKind"/>.
/// </summary>
public enum RaidPlanElementKind
{
    /// <summary>A raid target marker or spell icon.</summary>
    Icon = 1,

    /// <summary>A free-text label.</summary>
    Text = 2,

    /// <summary>An axis-aligned rectangle.</summary>
    Rectangle = 3,

    /// <summary>A circle/ellipse bounded by the element's box.</summary>
    Circle = 4,

    /// <summary>A triangle bounded by the element's box.</summary>
    Triangle = 5,

    /// <summary>A straight line between two points, no arrowhead.</summary>
    Line = 6,

    /// <summary>A straight line between two points with an arrowhead at the second point.</summary>
    Arrow = 7,
}
