using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A World of Warcraft expansion (Classic, TBC, WotLK, …).
/// Static seeded reference table — never modified at runtime.
/// </summary>
[Table("Expansions")]
public class Expansion
{
    /// <summary>
    /// Internal sequential identifier (1 = Classic, 2 = TBC, …).
    /// Assigned at seed time; never auto-incremented.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>Full display name, e.g. "The Burning Crusade".</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code for UI labels, e.g. "TBC", "WotLK".</summary>
    [Required, MaxLength(16)]
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>Chronological release order (ascending). Drives display ordering.</summary>
    public int ReleaseOrder { get; set; }
}
