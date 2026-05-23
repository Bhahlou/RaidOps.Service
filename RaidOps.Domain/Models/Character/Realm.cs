using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Character;

/// <summary>
/// A WoW realm (server) scoped to a branch and region.
/// Realms are fetched on-demand from the BNet API and cached in this table
/// (slug + name + region per branch).
/// </summary>
[Table("Realms")]
public class Realm
{
    /// <summary>Internal auto-incremented identifier.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// URL-safe slug used by the BNet API (e.g. "kazzak", "realm-of-chaos").
    /// Unique within (Branch, Region).
    /// </summary>
    [Required, MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Localised display name (e.g. "Kazzak", "Realm of Chaos").</summary>
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>BNet API region code: "us", "eu", "kr", "tw".</summary>
    [Required, MaxLength(4)]
    public string Region { get; set; } = string.Empty;

    /// <summary>FK to the branch this realm belongs to.</summary>
    public int BranchId { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The branch this realm belongs to.</summary>
    public virtual Branch Branch { get; set; } = null!;

    /// <summary>Characters whose home realm is this one.</summary>
    public virtual ICollection<Character> Characters { get; set; } = [];
}
