namespace RaidOps.Domain.Enums;

/// <summary>
/// Identifies the kind of in-app notification surfaced to a user. Notifications are never
/// persisted as event rows — they are derived live from current state on each <c>/me</c> call
/// and cross-referenced against a dismissal ledger keyed by this type.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// The user is an admin of a guild that has at least one active
    /// <see cref="Models.Discord.GuildBranch"/> with no <see cref="Models.Discord.GuildBranch.OfficerRoleIds"/>
    /// set yet, i.e. no Discord role has been designated as granting Officer access on that branch.
    /// </summary>
    BranchOfficerRolesNotConfigured = 1,

    /// <summary>
    /// The user is an admin of an already-configured guild that has no
    /// <see cref="Models.Discord.Guild.Language"/> set yet — it was added after some guilds had
    /// already registered, so their bot messages silently fall back to English instead of the
    /// guild's actual language.
    /// </summary>
    GuildLanguageNotConfigured = 2,

    /// <summary>
    /// The user is an admin of an already-configured guild that has never saved the "Absences"
    /// Discord notification family (neither <see cref="Models.Discord.GuildNotificationSetting"/>
    /// row for that family exists yet) — as opposed to a guild that deliberately saved the tab
    /// with every event left off, which is a legitimate steady state.
    /// </summary>
    AbsenceNotificationsNotConfigured = 3,

    /// <summary>
    /// The user is an admin of a guild that has at least one active
    /// <see cref="Models.Discord.GuildBranch"/> with no <see cref="Models.Discord.GuildBranch.Region"/>
    /// set yet — without it, the weekly raid-lockout reset window can't be resolved for that branch.
    /// </summary>
    BranchRegionNotConfigured = 4,
}
