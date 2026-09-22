using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A WoW spell/ability available for guilds to reference in their raid attribution templates
/// (buffs, curses, interrupts, cooldowns, …). Uses Blizzard's own spell ID as primary key, same
/// convention as <see cref="Spec"/>/<see cref="WowClass"/>. Static seeded reference table — never
/// modified at runtime, but unlike the other reference tables its seed data is loaded from a
/// checked-in JSON file via an idempotent startup upsert rather than EF <c>HasData</c>, since the
/// row count (thousands per expansion) doesn't fit in a generated migration file. Re-imported
/// once per expansion release, not incrementally maintained by guild officers.
/// </summary>
[Table("Spells")]
public class Spell
{
    /// <summary>Blizzard's spell ID. Assigned at import time; never auto-incremented.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>FK to the expansion this spell was imported for.</summary>
    public int ExpansionId { get; set; }

    /// <summary>English display name.</summary>
    [Required, MaxLength(128)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>French display name.</summary>
    [Required, MaxLength(128)]
    public string NameFr { get; set; } = string.Empty;

    /// <summary>German display name.</summary>
    [Required, MaxLength(128)]
    public string NameDe { get; set; } = string.Empty;

    /// <summary>
    /// Icon URL, hosted by RaidOps itself — downloaded once at import time rather than hotlinked
    /// from a third party, same principle applied to every other externally-sourced image asset.
    /// </summary>
    [Required, MaxLength(512)]
    public string IconUrl { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansion this spell was imported for.</summary>
    public virtual Expansion Expansion { get; set; } = null!;
}
