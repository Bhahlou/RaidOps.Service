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

    /// <summary>
    /// Whether this branch is offered for new activity — hidden from the character-sync branch
    /// picker and from a guild's "activate a new branch" list once false. Existing data referencing
    /// this branch (characters, GuildBranch activations) is never affected; only new sign-up surfaces
    /// stop showing it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether BNet character sync actually works for this branch yet. False for a branch that's
    /// been announced/seeded ahead of Blizzard shipping its BNet API support (e.g. a brand-new game
    /// branch in beta) — shown in the character-sync picker as "coming soon" rather than hidden
    /// outright, since the branch itself can still be activated on a guild and used for
    /// recruitment/planning in the meantime.
    /// </summary>
    public bool SyncAvailable { get; set; } = true;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansion currently active on this branch.</summary>
    public virtual Expansion CurrentExpansion { get; set; } = null!;
}
