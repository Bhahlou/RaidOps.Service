using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A live game branch (Retail, Classic Era, MoP Classic, …).
/// Each branch owns a realm pool and maps to a BNet API namespace prefix.
/// Static seeded reference table — never modified at runtime.
/// </summary>
[Table("Branches")]
public class Branch
{
    /// <summary>Internal sequential identifier. Assigned at seed time; never auto-incremented.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>Display name shown in the character picker (e.g. "Classic Era", "MoP Classic").</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// BNet API namespace prefix — append "-{region}" at query time to build the full namespace.
    /// Examples: "dynamic" (Retail), "dynamic-classic1x" (Classic Era).
    /// </summary>
    [Required, MaxLength(32)]
    public string BnetNamespacePrefix { get; set; } = string.Empty;

    /// <summary>FK to the expansion that is currently active / end-game on this branch.</summary>
    public int CurrentExpansionId { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansion currently active on this branch.</summary>
    public virtual Expansion CurrentExpansion { get; set; } = null!;
}
