namespace RaidOps.Domain.Enums;

/// <summary>
/// The trinity role that a specialization fulfils in a raid or group.
/// </summary>
public enum SpecRole
{
    /// <summary>Absorbs or mitigates damage for the group.</summary>
    Tank = 1,

    /// <summary>Restores health to group members.</summary>
    Healer = 2,

    /// <summary>Deals damage to enemies.</summary>
    Dps = 3
}
