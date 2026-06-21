using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Character;

/// <summary>
/// A spec a player has manually declared as viable to raid with on a given character.
/// Set by the user at activation (and editable later) — never recalculated or
/// overwritten by Battle.net sync, unlike <see cref="BnetCharacterSpec"/>.
/// Composite primary key: (<see cref="CharacterId"/>, <see cref="SpecId"/>).
/// </summary>
[Table("CharacterRaidSpecs")]
public class CharacterRaidSpec
{
    /// <summary>FK to the character this entry belongs to.</summary>
    public int CharacterId { get; set; }

    /// <summary>FK to the specialisation.</summary>
    public int SpecId { get; set; }

    /// <summary>
    /// Whether this is the character's main raid spec.
    /// Exactly one spec per character should have <c>IsMain = true</c>.
    /// </summary>
    public bool IsMain { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The character this entry belongs to.</summary>
    public virtual Character Character { get; set; } = null!;

    /// <summary>The specialisation.</summary>
    public virtual Spec Spec { get; set; } = null!;
}
