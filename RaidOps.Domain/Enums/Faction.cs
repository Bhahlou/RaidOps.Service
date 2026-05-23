namespace RaidOps.Domain.Enums;

/// <summary>
/// Faction alignment for a WoW race or character.
/// </summary>
public enum Faction
{
    /// <summary>Alliance faction.</summary>
    Alliance = 1,

    /// <summary>Horde faction.</summary>
    Horde = 2,

    /// <summary>
    /// Neutral — the race can join either faction (e.g. Pandaren).
    /// The character's actual faction is stored on the character itself.
    /// </summary>
    Neutral = 3
}
