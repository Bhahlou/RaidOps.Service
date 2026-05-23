using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A playable WoW race (Human, Orc, Pandaren, Dracthyr, …).
/// Uses Blizzard's official race ID as primary key so it maps directly
/// to the <c>race.id</c> field returned by the BNet character profile API.
/// Static seeded reference table — never modified at runtime.
/// </summary>
[Table("Races")]
public class Race
{
    /// <summary>
    /// Blizzard's race ID — matches the <c>race.id</c> field in BNet API responses.
    /// Assigned at seed time; never auto-incremented.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Blood Elf", "Kul Tiran").</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Default faction for this race. Dual-faction and neutral races use <see cref="Faction.Neutral"/>.</summary>
    public Faction Faction { get; set; }

    /// <summary>The expansion in which this race first became playable.</summary>
    public int FirstExpansionId { get; set; }
}
