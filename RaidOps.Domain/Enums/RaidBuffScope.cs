namespace RaidOps.Domain.Enums;

/// <summary>How far a raid buff or debuff reaches once a member of the composition provides it.</summary>
public enum RaidBuffScope
{
    /// <summary>Applies to the whole raid (or to the boss, for a debuff).</summary>
    Raid = 0,

    /// <summary>Applies only to the provider's own party of five.</summary>
    Group = 1,

    /// <summary>Cast on a single chosen target (e.g. Innervate, Power Infusion).</summary>
    Individual = 2,
}
