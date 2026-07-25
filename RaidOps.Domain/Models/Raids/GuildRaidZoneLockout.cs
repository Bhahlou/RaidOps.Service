using System.ComponentModel.DataAnnotations;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A guild-specific correction to a <see cref="RaidZone"/>'s lockout baseline. Exists because reset
/// day/cadence can differ by region/realm (e.g. TBC resets Wednesday on EU, a different day on other
/// regions) even though the zone's seeded <see cref="RaidZone.LockoutAnchorDate"/> and
/// <see cref="RaidZone.LockoutCadenceDays"/> are shared reference data across every guild. When a row
/// exists for a (guild, zone) pair, its non-null fields override the zone's baseline for that guild
/// only; a <c>null</c> field falls back to the zone's value. Distinct from
/// <see cref="RaidLockoutCadenceOverride"/>, which corrects a temporary, zone-wide anomaly shared by
/// every guild rather than a permanent per-guild baseline difference.
/// </summary>
public class GuildRaidZoneLockout
{
    /// <summary>Discord snowflake ID of the guild this correction applies to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>FK to the raid zone this correction applies to.</summary>
    public int RaidZoneId { get; set; }

    /// <summary>Guild-specific reset anchor date, or <c>null</c> to use the zone's baseline.</summary>
    public DateOnly? LockoutAnchorDate { get; set; }

    /// <summary>Guild-specific reset cadence in days, or <c>null</c> to use the zone's baseline.</summary>
    public int? LockoutCadenceDays { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this correction applies to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The raid zone this correction applies to.</summary>
    public virtual RaidZone RaidZone { get; set; } = null!;
}
