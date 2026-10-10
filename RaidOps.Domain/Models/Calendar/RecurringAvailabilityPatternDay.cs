using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Calendar;

/// <summary>
/// A single offset within a <see cref="RecurringAvailabilityPattern"/>'s cycle that is not fully
/// available. Storage is sparse — an offset with no row is fully available.
/// </summary>
[Table("RecurringAvailabilityPatternDays")]
public class RecurringAvailabilityPatternDay
{
    /// <summary>Auto-incremented primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the parent pattern.</summary>
    public int PatternId { get; set; }

    /// <summary>Zero-based offset within the pattern's cycle (e.g. 2 = the third day of the cycle).</summary>
    public int OffsetInCycle { get; set; }

    /// <summary>Declared status for this offset. Only <see cref="DayAvailabilityStatus.Absent"/> or <see cref="DayAvailabilityStatus.Partial"/> are meaningful here.</summary>
    public DayAvailabilityStatus Status { get; set; }

    /// <summary>Optional free-text reason (e.g. "nuit", "poste du matin").</summary>
    [MaxLength(256)]
    public string? Reason { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available. Null if not bounded on this side.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available. Null if not bounded on this side.</summary>
    public TimeOnly? AvailableUntil { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The parent pattern this day belongs to.</summary>
    public virtual RecurringAvailabilityPattern Pattern { get; set; } = null!;
}
