namespace RaidOps.Domain.Enums;

/// <summary>
/// The role that a specialization fulfils in a raid or group — a 4-way split (not the classic
/// tank/healer/dps trinity) since melee and ranged dps are meaningfully different for raid
/// composition and attribution-slot eligibility (e.g. "must be a ranged dps").
/// </summary>
public enum SpecRole
{
    /// <summary>Absorbs or mitigates damage for the group.</summary>
    Tank = 1,

    /// <summary>Restores health to group members.</summary>
    Healer = 2,

    /// <summary>Deals damage to enemies from range.</summary>
    RangedDps = 3,

    /// <summary>Deals damage to enemies in melee.</summary>
    MeleeDps = 4,
}
