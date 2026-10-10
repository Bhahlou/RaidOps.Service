using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A boss encounter within a <see cref="RaidZone"/> (e.g. "Hydross the Unstable" in Serpentshrine
/// Cavern). Static seeded reference table — never modified at runtime, same convention as
/// <see cref="RaidZone"/> itself. Drives the boss-scoped attribution templates
/// (<see cref="Attributions.GuildAttributionDefinition.RaidBossId"/>) and the boss navigation on a
/// raid event's Assignments page.
/// </summary>
[Table("RaidBosses")]
public class RaidBoss
{
    /// <summary>Internal sequential identifier. Assigned at seed time; never auto-incremented.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>FK to the raid zone this boss belongs to.</summary>
    public int RaidZoneId { get; set; }

    /// <summary>Display name (e.g. "Hydross the Unstable").</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Icon URL (boss portrait), or <c>null</c> if none is configured.</summary>
    [MaxLength(512)]
    public string? IconUrl { get; set; }

    /// <summary>Display/pull ordering within its zone.</summary>
    public int SortOrder { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The raid zone this boss belongs to.</summary>
    public virtual RaidZone RaidZone { get; set; } = null!;
}
