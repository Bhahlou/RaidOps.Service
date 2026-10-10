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

    /// <summary>
    /// This branch's product code on wago.tools (e.g. <c>"wow_classic_beta"</c> for Forever), used by
    /// the spell-sync background service to poll <c>/api/builds/latest</c> and pull DB2 exports for
    /// the right product. Null for a branch that isn't tracked (e.g. the deactivated Classic Era).
    /// </summary>
    [MaxLength(32)]
    public string? WagoProductCode { get; set; }

    /// <summary>
    /// The wago.tools build version (e.g. <c>"1.60.1.69977"</c>) this branch's spell data was last
    /// synced from. Null until the first successful sync.
    /// </summary>
    [MaxLength(32)]
    public string? LastSyncedBuildVersion { get; set; }

    /// <summary>
    /// The synced build's own <c>created_at</c> timestamp (UTC) from wago.tools — when Blizzard
    /// shipped that build, not when RaidOps processed it.
    /// </summary>
    public DateTime? LastSyncedBuildDate { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansion currently active on this branch.</summary>
    public virtual Expansion CurrentExpansion { get; set; } = null!;
}
