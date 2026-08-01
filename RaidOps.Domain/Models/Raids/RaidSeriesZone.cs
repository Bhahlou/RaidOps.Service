using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// Join row linking a <see cref="RaidSeries"/> to one of the <see cref="RaidZone"/>s its
/// materialized occurrences target by default (e.g. a "Split 1" series targeting both SSC and TK
/// with a single grid). Composite primary key: (<see cref="RaidSeriesId"/>, <see cref="RaidZoneId"/>).
/// </summary>
[Table("RaidSeriesZones")]
public class RaidSeriesZone
{
    /// <summary>FK to the parent series.</summary>
    public int RaidSeriesId { get; set; }

    /// <summary>FK to the targeted raid zone.</summary>
    public int RaidZoneId { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The parent series.</summary>
    public virtual RaidSeries RaidSeries { get; set; } = null!;

    /// <summary>The targeted raid zone.</summary>
    public virtual RaidZone RaidZone { get; set; } = null!;
}
