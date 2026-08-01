using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A raid instance (Karazhan, SSC, Black Temple, …) available on a given <see cref="Expansion"/>.
/// Static seeded reference table — never modified at runtime, same convention as <see cref="Spec"/>
/// or <see cref="Branch"/>. Drives the group/slot grid size offered when building a
/// <c>RaidSeries</c>/<c>RaidEvent</c> and the cadence used by the lockout engine.
/// </summary>
[Table("RaidZones")]
public class RaidZone
{
    /// <summary>Internal sequential identifier. Assigned at seed time; never auto-incremented.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Serpentshrine Cavern").</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code for compact UI labels (e.g. "SSC").</summary>
    [Required, MaxLength(16)]
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>FK to the expansion this raid zone belongs to.</summary>
    public int ExpansionId { get; set; }

    /// <summary>Number of groups in the raid grid (e.g. 5 for a 25-man raid, 2 for a 10-man).</summary>
    public int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public int SlotsPerGroup { get; set; }

    /// <summary>
    /// Number of days between lockout resets for this zone, independently of the region's weekly
    /// reset — e.g. 3 for Zul'Gurub/AQ20, 5 for Onyxia. <c>null</c> means this zone simply follows
    /// its guild branch's <see cref="WeeklyLockoutSchedule"/> (the common case — every post-Vanilla
    /// raid resets with the weekly reset). Ignored when non-null, absent any active
    /// <see cref="RaidLockoutCadenceOverride"/> covering the date being evaluated.
    /// </summary>
    public int? LockoutCadenceDays { get; set; }

    /// <summary>
    /// UTC origin instant for this zone's own lockout cadence — only meaningful when
    /// <see cref="LockoutCadenceDays"/> is set. Any genuine reset instant works, since the engine
    /// advances from it in whole-cadence jumps.
    /// </summary>
    public DateTime? LockoutAnchorUtc { get; set; }

    /// <summary>Icon URL, or <c>null</c> if none is configured.</summary>
    [MaxLength(512)]
    public string? IconUrl { get; set; }

    /// <summary>Display ordering within its expansion.</summary>
    public int SortOrder { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansion this raid zone belongs to.</summary>
    public virtual Expansion Expansion { get; set; } = null!;

    /// <summary>Time-bound cadence corrections for this zone (e.g. a temporary anomaly period).</summary>
    public virtual ICollection<RaidLockoutCadenceOverride> LockoutOverrides { get; set; } = [];
}
