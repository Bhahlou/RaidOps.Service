using System.ComponentModel.DataAnnotations;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// What <see cref="Spell"/> <see cref="SpellId"/> is called and looks like on
/// <see cref="Expansion"/> <see cref="ExpansionId"/> — a many-to-many join that also carries the
/// per-expansion content, since Blizzard spell IDs are reused/cumulative across expansions and
/// branches but names and icons are not stable across them (the same ID can be a renamed spell, or
/// even a different one, on Classic vs Retail). Composite primary key, no surrogate ID needed since
/// the (spell, expansion) pair identifies the row.
/// </summary>
public class SpellAvailability
{
    /// <summary>FK to the spell.</summary>
    public int SpellId { get; set; }

    /// <summary>FK to the expansion this spell has been observed on.</summary>
    public int ExpansionId { get; set; }

    /// <summary>English display name on this expansion.</summary>
    [Required, MaxLength(128)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>French display name on this expansion.</summary>
    [Required, MaxLength(128)]
    public string NameFr { get; set; } = string.Empty;

    /// <summary>German display name on this expansion.</summary>
    [Required, MaxLength(128)]
    public string NameDe { get; set; } = string.Empty;

    /// <summary>
    /// Icon URL on this expansion — a direct <c>render.worldofwarcraft.com</c> link (Blizzard's own
    /// icon render CDN), not re-hosted by RaidOps. Empty when the spell has no resolvable icon.
    /// </summary>
    [Required, MaxLength(512)]
    public string IconUrl { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The spell.</summary>
    public virtual Spell Spell { get; set; } = null!;

    /// <summary>The expansion.</summary>
    public virtual Expansion Expansion { get; set; } = null!;
}
