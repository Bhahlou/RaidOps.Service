using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Character;

/// <summary>
/// A snapshot of a character's progress within a specific expansion.
/// One row per (character × expansion) pair, created when the character is
/// imported or refreshed via the BNet API.
/// </summary>
[Table("CharacterExpansionStates")]
public class CharacterExpansionState
{
    /// <summary>Internal auto-incremented surrogate key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the character this state belongs to.</summary>
    public int CharacterId { get; set; }

    /// <summary>FK to the expansion this snapshot covers.</summary>
    public int ExpansionId { get; set; }

    /// <summary>
    /// Character level at the end of this expansion's content,
    /// or the current level for an active branch.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Average equipped item level, if available from the BNet API.
    /// <c>null</c> for expansions where item level data is not exposed.
    /// </summary>
    public int? ItemLevel { get; set; }

    /// <summary>
    /// Whether this expansion state is the character's currently active one
    /// (the branch is live and the character has been played in this expansion).
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// In-game guild name at the time of last activation or sync.
    /// <c>null</c> if the character has no guild or the API call failed.
    /// </summary>
    [MaxLength(64)]
    public string? GuildName { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The character this state belongs to.</summary>
    public virtual Character Character { get; set; } = null!;

    /// <summary>The expansion this snapshot covers.</summary>
    public virtual Expansion Expansion { get; set; } = null!;

    /// <summary>The specialisations the character has played in this expansion, as reported by Battle.net.</summary>
    public virtual ICollection<BnetCharacterSpec> Specs { get; set; } = [];
}
