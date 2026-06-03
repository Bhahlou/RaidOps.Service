using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A playable WoW class (Warrior, Paladin, Evoker, …).
/// Uses Blizzard's official class ID as primary key so it maps directly
/// to the <c>character_class.id</c> field returned by the BNet character profile API.
/// Static seeded reference table — never modified at runtime.
/// </summary>
[Table("WowClasses")]
public class WowClass
{
    /// <summary>
    /// Blizzard's class ID — matches the <c>character_class.id</c> field in BNet API responses.
    /// Assigned at seed time; never auto-incremented.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Death Knight", "Demon Hunter").</summary>
    [Required, MaxLength(32)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Official class colour as a 6-character hex string (no leading #),
    /// used for UI colouring (e.g. "C41F3B" for Death Knight).
    /// </summary>
    [Required, MaxLength(6)]
    public string Color { get; set; } = string.Empty;

    /// <summary>The expansion in which this class first became playable.</summary>
    public int FirstExpansionId { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>Specialisations belonging to this class.</summary>
    public virtual ICollection<Spec> Specs { get; set; } = [];
}
