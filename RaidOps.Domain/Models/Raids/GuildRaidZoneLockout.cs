using System.ComponentModel.DataAnnotations;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A guild-specific correction to a <see cref="RaidZone"/>'s lockout baseline — for exotic cases
/// (e.g. a private/anomalous server) where a guild's actual reset doesn't match either its branch's
/// <see cref="WeeklyLockoutSchedule"/> or the zone's own <see cref="RaidZone.LockoutCadenceDays"/>.
/// The normal per-region difference (EU vs. US/Latam/Oceania reset day) is handled by
/// <see cref="GuildBranch.Region"/>, not by this table. When a row exists for a (guild, zone)
/// pair, its non-null fields override the resolved baseline for that guild only; a <c>null</c> field
/// falls back to the normal resolution. Distinct from <see cref="RaidLockoutCadenceOverride"/>, which
/// corrects a temporary, zone-wide anomaly shared by every guild rather than a permanent per-guild
/// baseline difference.
/// </summary>
public class GuildRaidZoneLockout
{
    /// <summary>Discord snowflake ID of the guild this correction applies to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>FK to the raid zone this correction applies to.</summary>
    public int RaidZoneId { get; set; }

    /// <summary>Guild-specific reset anchor instant (UTC), or <c>null</c> to use the resolved baseline.</summary>
    public DateTime? LockoutAnchorUtc { get; set; }

    /// <summary>Guild-specific reset cadence in days, or <c>null</c> to use the resolved baseline.</summary>
    public int? LockoutCadenceDays { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this correction applies to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The raid zone this correction applies to.</summary>
    public virtual RaidZone RaidZone { get; set; } = null!;
}
