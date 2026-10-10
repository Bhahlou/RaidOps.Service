namespace RaidOps.Domain.Enums;

/// <summary>Whether a <see cref="Models.Reference.RaidBuffDefinition"/> helps the raid or hinders the boss.</summary>
public enum RaidBuffKind
{
    /// <summary>A beneficial effect on friendly targets.</summary>
    Buff = 0,

    /// <summary>A harmful effect on the enemy (armor reduction, curse, …).</summary>
    Debuff = 1,
}
