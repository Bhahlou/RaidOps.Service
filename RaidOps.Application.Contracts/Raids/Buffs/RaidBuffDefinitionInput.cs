using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Buffs;

/// <summary>
/// The editable fields of a raid buff/debuff definition, keyed by (expansion, <see cref="SpellId"/>).
/// This is also the JSON shape of the admin export/import file, so it carries nothing environment
/// specific: spells, classes and specs are all identified by Blizzard's own IDs.
/// </summary>
public class RaidBuffDefinitionInput
{
    /// <summary>Blizzard spell ID of the spell providing the effect.</summary>
    public int SpellId { get; set; }

    /// <summary>How far the effect reaches — whole raid, the provider's party, or a single target.</summary>
    public RaidBuffScope Scope { get; set; }

    /// <summary>Whether the effect is a buff on friendly targets or a debuff on the enemy.</summary>
    public RaidBuffKind Kind { get; set; }

    /// <summary>English effect label (e.g. "+25% armor").</summary>
    public string LabelEn { get; set; } = string.Empty;

    /// <summary>French effect label.</summary>
    public string LabelFr { get; set; } = string.Empty;

    /// <summary>German effect label.</summary>
    public string LabelDe { get; set; } = string.Empty;

    /// <summary>Definitions sharing this key are non-stacking alternatives for one effect. <c>null</c> = stands alone.</summary>
    public string? ExclusiveGroupKey { get; set; }

    /// <summary>Definitions sharing this key form a capacity pool (one provider covers one of them). <c>null</c> = no pooling.</summary>
    public string? CapacityPoolKey { get; set; }

    /// <summary>Display order within the expansion; also decides which pool members are covered first.</summary>
    public int SortOrder { get; set; }

    /// <summary>The classes/specs able to provide the effect — any one of them is enough. At least one is required.</summary>
    public List<RaidBuffSourceDto> Sources { get; set; } = [];
}
