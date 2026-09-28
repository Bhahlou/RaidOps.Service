using System.ComponentModel.DataAnnotations;
using RaidOps.Domain.Enums;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A curated raid, group or individual buff/debuff a raid composition can bring, for one expansion —
/// what the raid composition preview checks a composition against. Maintained by hand through the
/// owner-only admin screen (or its JSON import), never synced: which of the thousands of spells are
/// relevant is an editorial call. Each row references one spell; two spells granting the same effect
/// (e.g. Expose Armor and Sunder Armor) are two rows sharing an <see cref="ExclusiveGroupKey"/>.
/// </summary>
public class RaidBuffDefinition
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>FK to the expansion this definition applies to (a Forever list is not a Classic list).</summary>
    public int ExpansionId { get; set; }

    /// <summary>
    /// FK to the spell providing the effect. A real FK (unlike a seeded reference row), so the spell
    /// must already have been synced from wago.tools; name and icon are resolved through the spell's
    /// <see cref="SpellAvailability"/> on <see cref="ExpansionId"/>.
    /// </summary>
    public int SpellId { get; set; }

    /// <summary>How far the effect reaches — whole raid, the provider's party, or a single target.</summary>
    public RaidBuffScope Scope { get; set; }

    /// <summary>Whether the effect is a buff on friendly targets or a debuff on the enemy.</summary>
    public RaidBuffKind Kind { get; set; }

    /// <summary>English effect label (e.g. "+25% armor").</summary>
    [Required, MaxLength(128)]
    public string LabelEn { get; set; } = string.Empty;

    /// <summary>French effect label.</summary>
    [Required, MaxLength(128)]
    public string LabelFr { get; set; } = string.Empty;

    /// <summary>German effect label.</summary>
    [Required, MaxLength(128)]
    public string LabelDe { get; set; } = string.Empty;

    /// <summary>
    /// Definitions sharing the same key (within an expansion) are alternatives for one effect that don't
    /// stack, so a composition providing any one of them covers the effect. <c>null</c> = stands alone.
    /// </summary>
    [MaxLength(64)]
    public string? ExclusiveGroupKey { get; set; }

    /// <summary>
    /// Definitions sharing the same key form a capacity pool: each provider in the composition can only
    /// cover one of them (a paladin casts one blessing per target), so N providers light up the first N
    /// definitions by <see cref="SortOrder"/>. <c>null</c> = no pooling, presence alone is enough.
    /// </summary>
    [MaxLength(64)]
    public string? CapacityPoolKey { get; set; }

    /// <summary>Display order within the expansion; also decides which pool members are covered first.</summary>
    public int SortOrder { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansion.</summary>
    public virtual Expansion Expansion { get; set; } = null!;

    /// <summary>The spell providing the effect.</summary>
    public virtual Spell Spell { get; set; } = null!;

    /// <summary>The classes/specs able to provide the effect — any one of them is enough.</summary>
    public virtual ICollection<RaidBuffSource> Sources { get; set; } = [];
}
