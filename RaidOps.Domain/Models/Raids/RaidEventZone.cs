using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// Join row linking a <see cref="RaidEvent"/> to one of the <see cref="RaidZone"/>s it targets.
/// Composite primary key: (<see cref="RaidEventId"/>, <see cref="RaidZoneId"/>).
/// </summary>
[Table("RaidEventZones")]
public class RaidEventZone
{
    /// <summary>FK to the parent event.</summary>
    public int RaidEventId { get; set; }

    /// <summary>FK to the targeted raid zone.</summary>
    public int RaidZoneId { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The parent event.</summary>
    public virtual RaidEvent RaidEvent { get; set; } = null!;

    /// <summary>The targeted raid zone.</summary>
    public virtual RaidZone RaidZone { get; set; } = null!;
}
