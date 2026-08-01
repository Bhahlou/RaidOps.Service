using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A time-bound correction to a <see cref="RaidZone"/>'s normal lockout cadence (e.g. a raid that
/// reset every 3 days for 3 weeks due to a server anomaly, instead of its usual weekly cadence).
/// Versioned the same way as <c>RecurringAvailabilityPattern</c>: bounded by
/// <see cref="EffectiveFrom"/>/<see cref="EffectiveUntil"/> rather than mutating the zone's baseline.
/// Global per zone (shared by every guild using it) — not scoped to a realm/region, and not
/// exposed through any officer-facing CRUD in this milestone; rows are inserted directly.
/// </summary>
[Table("RaidLockoutCadenceOverrides")]
public class RaidLockoutCadenceOverride
{
    /// <summary>Auto-incremented primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the raid zone this override applies to.</summary>
    public int RaidZoneId { get; set; }

    /// <summary>Number of days between lockout resets while this override is active.</summary>
    public int CadenceDays { get; set; }

    /// <summary>First date (inclusive) this override applies from.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Last date (inclusive) this override applies until, or <c>null</c> if still open-ended.</summary>
    public DateOnly? EffectiveUntil { get; set; }

    /// <summary>Optional free-text explanation for the correction (e.g. "reset anomaly Jan 2026").</summary>
    [MaxLength(256)]
    public string? Reason { get; set; }

    /// <summary>Discord snowflake ID of the user who inserted this override.</summary>
    [Required]
    public string CreatedByDiscordId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when this override was inserted.</summary>
    public DateTime CreatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The raid zone this override applies to.</summary>
    public virtual RaidZone RaidZone { get; set; } = null!;
}
