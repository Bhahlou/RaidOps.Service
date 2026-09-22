using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Raids.Plans;

/// <summary>
/// A guild's visual strategy board for one specific boss encounter (raidplan.io-style: a
/// background image with draggable icons/text/shapes/arrows on top, organized into named
/// <see cref="RaidPlanPage"/>s — typically one per fight phase). Always boss-specific, unlike
/// <see cref="Attributions.GuildAttributionDefinition"/> which also supports a "General" scope —
/// a strategy board only ever makes sense for one encounter.
/// </summary>
[Table("RaidPlans")]
public class RaidPlan
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the guild this board belongs to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>FK to the boss this board is for.</summary>
    public int RaidBossId { get; set; }

    /// <summary>
    /// Display name (e.g. "Add positioning"). A boss can have more than one board — different
    /// strategies for the same fight — though the V1 editor only ever surfaces one per boss.
    /// </summary>
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>UTC timestamp this board was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who created this board.</summary>
    [Required]
    public string CreatedByDiscordId { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this board belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The boss this board is for.</summary>
    public virtual RaidBoss RaidBoss { get; set; } = null!;

    /// <summary>This board's pages (typically one per phase), in display order.</summary>
    public virtual ICollection<RaidPlanPage> Pages { get; set; } = [];
}
