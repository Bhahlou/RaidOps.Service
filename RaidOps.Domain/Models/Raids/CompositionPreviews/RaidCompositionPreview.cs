using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Raids.CompositionPreviews;

/// <summary>
/// A named, officer-authored simulation of what a raid composition could look like — class/spec
/// placeholders (plus a free-text note) per grid slot, with no real <see cref="Character.Character"/>
/// involved. Built for planning ahead of real roster data (e.g. before a new branch's characters
/// can be imported), so it deliberately shares no shape with <see cref="RaidEvent"/> beyond the
/// group/slot grid convention (no date, lockout, signup, or publication concept). A guild branch
/// can have several named previews (e.g. one per format, or several drafts of the same format).
/// </summary>
[Table("RaidCompositionPreviews")]
public class RaidCompositionPreview
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the guild branch this preview belongs to.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Display name (e.g. "40-man target comp").</summary>
    [Required, MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of groups in the grid — freely chosen between 1 and 8 at creation (not tied to a
    /// fixed 10/20/40 format), so a hypothetical future raid size needs no code change.
    /// </summary>
    public int GroupCount { get; set; }

    /// <summary>Number of slots per group in the grid — always 5, the constant WoW group size.</summary>
    public int SlotsPerGroup { get; set; }

    /// <summary>UTC timestamp this preview was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who created this preview.</summary>
    [Required]
    public string CreatedByDiscordId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last update (rename or any slot change), or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild branch this preview belongs to.</summary>
    public virtual GuildBranch GuildBranch { get; set; } = null!;

    /// <summary>This preview's sparse slots — a coordinate with no row is an empty slot.</summary>
    public virtual ICollection<RaidCompositionPreviewSlot> Slots { get; set; } = [];
}
