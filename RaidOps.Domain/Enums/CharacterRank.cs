namespace RaidOps.Domain.Enums;

/// <summary>
/// Raid-composition rank assigned to a character on the guild roster.
/// Determines how the character is treated when building raid teams.
/// </summary>
public enum CharacterRank
{
    /// <summary>Main character — highest priority for raid spots.</summary>
    Main = 1,

    /// <summary>Split-run character — used for alt/split raids to farm gear for the main.</summary>
    Split = 2,

    /// <summary>Alt character — optional attendee, lower priority.</summary>
    Alt = 3,
}
