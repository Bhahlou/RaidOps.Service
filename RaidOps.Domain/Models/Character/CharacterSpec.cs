using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Character;

/// <summary>
/// Links a <see cref="Spec"/> to a <see cref="CharacterExpansionState"/>.
/// A character can have multiple specs per expansion (e.g. main + off-spec).
/// Composite primary key: (<see cref="CharacterExpansionStateId"/>, <see cref="SpecId"/>, <see cref="IsMain"/>)
/// — <see cref="IsMain"/> is part of the PK to allow Classic same-spec dual-spec (e.g. Ret/Ret).
/// </summary>
[Table("CharacterSpecs")]
public class CharacterSpec
{
    /// <summary>FK to the expansion state this spec entry belongs to.</summary>
    public int CharacterExpansionStateId { get; set; }

    /// <summary>FK to the specialisation.</summary>
    public int SpecId { get; set; }

    /// <summary>
    /// Whether this is the character's primary / preferred spec in this expansion.
    /// At most one spec per expansion state should have <c>IsMain = true</c>.
    /// </summary>
    public bool IsMain { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansion state this spec entry belongs to.</summary>
    public virtual CharacterExpansionState CharacterExpansionState { get; set; } = null!;

    /// <summary>The specialisation.</summary>
    public virtual Spec Spec { get; set; } = null!;
}
