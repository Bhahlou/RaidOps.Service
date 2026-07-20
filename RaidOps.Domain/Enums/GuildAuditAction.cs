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

    /// <summary>The guild's Officer role threshold was updated.</summary>
    OfficerThresholdUpdated = 7,

    /// <summary>A member declared a one-off availability exception (absence/lateness).</summary>
    AvailabilityExceptionDeclared = 8,

    /// <summary>A member deleted a one-off availability exception.</summary>
    AvailabilityExceptionDeleted = 9,

    /// <summary>A member created a recurring availability pattern.</summary>
    RecurringAvailabilityPatternCreated = 10,

    /// <summary>A member edited a recurring availability pattern (non-retroactively).</summary>
    RecurringAvailabilityPatternUpdated = 11,

    /// <summary>A member stopped a recurring availability pattern (non-retroactively).</summary>
    RecurringAvailabilityPatternStopped = 12,
}
