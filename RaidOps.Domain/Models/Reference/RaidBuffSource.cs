namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// One class/spec able to provide a <see cref="RaidBuffDefinition"/>. A definition lists several of
/// these when alternatives exist ("Feral or Guardian Druid"); a member of the composition matching
/// any one of them provides the effect.
/// </summary>
public class RaidBuffSource
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>FK to the definition this source belongs to.</summary>
    public int RaidBuffDefinitionId { get; set; }

    /// <summary>FK to the providing class.</summary>
    public int ClassId { get; set; }

    /// <summary>FK to the providing spec, or <c>null</c> when any spec of <see cref="ClassId"/> provides it.</summary>
    public int? SpecId { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The definition this source belongs to.</summary>
    public virtual RaidBuffDefinition RaidBuffDefinition { get; set; } = null!;

    /// <summary>The providing class.</summary>
    public virtual WowClass Class { get; set; } = null!;

    /// <summary>The providing spec, when restricted to one.</summary>
    public virtual Spec? Spec { get; set; }
}
