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

    /// <summary>The guild's Discord notification settings (per-event enabled state and channel) were updated.</summary>
    NotificationSettingsUpdated = 13,

    /// <summary>A WoW game-version branch was activated on the guild.</summary>
    BranchActivated = 14,

    /// <summary>A WoW game-version branch was deactivated on the guild.</summary>
    BranchDeactivated = 15,

    /// <summary>A guild branch's roster/officer role-set configuration was updated.</summary>
    BranchRosterSettingsUpdated = 16,

    /// <summary>A branch's Discord notification settings overrides were reset, reverting it to the guild-wide fallback.</summary>
    NotificationSettingsReset = 17,
}
