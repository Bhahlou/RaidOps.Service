namespace RaidOps.Domain.Enums;

/// <summary>
/// Identifies the type of action recorded in the guild audit log.
/// </summary>
public enum GuildAuditAction
{
    /// <summary>A guild was registered in RaidOps.</summary>
    GuildRegistered = 1,

    /// <summary>Guild settings were updated (timezone, roster mode, role threshold).</summary>
    SettingsUpdated = 2,

    /// <summary>A character joined the guild roster.</summary>
    MemberJoined = 3,

    /// <summary>A character voluntarily left the guild roster.</summary>
    MemberLeft = 4,

    /// <summary>A character was removed from the guild roster by an officer.</summary>
    MemberExcluded = 5,

    /// <summary>A character's raid-composition rank was changed.</summary>
    MemberRankUpdated = 6,
}
