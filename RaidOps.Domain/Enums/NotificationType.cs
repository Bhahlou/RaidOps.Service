namespace RaidOps.Domain.Enums;

/// <summary>
/// Identifies the kind of in-app notification surfaced to a user. Notifications are never
/// persisted as event rows — they are derived live from current state on each <c>/me</c> call
/// and cross-referenced against a dismissal ledger keyed by this type.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// The user is an admin of an already-configured guild that has no
    /// <see cref="Models.Discord.Guild.MinOfficerRoleId"/> set yet, i.e. no Discord role has
    /// been designated as granting Officer access.
    /// </summary>
    OfficerThresholdNotConfigured = 1,
}
