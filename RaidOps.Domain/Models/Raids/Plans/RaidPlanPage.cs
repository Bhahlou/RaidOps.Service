using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Raids.Plans;

/// <summary>
/// One named page of a <see cref="RaidPlan"/> (e.g. "Phase 1" / "Phase 2") — its own background
/// image and its own set of <see cref="RaidPlanElement"/>s. Officers add/rename/reorder/delete
/// pages as the strategy evolves.
/// </summary>
[Table("RaidPlanPages")]
public class RaidPlanPage
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the board this page belongs to.</summary>
    public int RaidPlanId { get; set; }

    /// <summary>Display name (e.g. "Phase 1").</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Display/tab ordering within the board, densely renumbered on every insert/reorder.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Key into the front-end's bundled background-image manifest, or <c>null</c> if no background
    /// has been picked yet. Deliberately not a URL — backgrounds are a curated, built-in library,
    /// never an arbitrary/external image.
    /// </summary>
    [MaxLength(128)]
    public string? BackgroundImageKey { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The board this page belongs to.</summary>
    public virtual RaidPlan RaidPlan { get; set; } = null!;

    /// <summary>This page's canvas elements.</summary>
    public virtual ICollection<RaidPlanElement> Elements { get; set; } = [];
}
