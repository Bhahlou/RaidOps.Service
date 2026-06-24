using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A playable class specialisation (Arms Warrior, Holy Paladin, Augmentation Evoker, …).
/// Uses Blizzard's official specialisation ID as primary key so it maps directly
/// to the <c>active_spec.id</c> field returned by the BNet character profile API.
/// Static seeded reference table — never modified at runtime.
/// </summary>
[Table("Specs")]
public class Spec
{
    /// <summary>
    /// Blizzard's specialisation ID — matches the <c>active_spec.id</c> field in BNet API responses.
    /// Assigned at seed time; never auto-incremented.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Arms", "Devastation", "Augmentation").</summary>
    [Required, MaxLength(32)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The raid/group role this spec fills: Tank, Healer, or DPS.</summary>
    public SpecRole Role { get; set; }

    /// <summary>FK to the class this spec belongs to.</summary>
    public int ClassId { get; set; }

    /// <summary>The expansion in which this spec first became available as a distinct specialisation.</summary>
    public int FirstExpansionId { get; set; }

    /// <summary>
    /// Icon URL from Blizzard's render service. Static seed value (see <c>SeedSpecs</c>) —
    /// spec icons essentially never change, so there is no runtime sync for this field.
    /// </summary>
    [MaxLength(512)]
    public string? IconUrl { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The class this spec belongs to.</summary>
    public virtual WowClass Class { get; set; } = null!;
}
