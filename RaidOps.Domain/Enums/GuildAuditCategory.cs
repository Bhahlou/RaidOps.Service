namespace RaidOps.Domain.Enums;

/// <summary>
/// Broad grouping over <see cref="GuildAuditAction"/>, used to make the audit log easier to
/// filter and scan as more action types get added over time.
/// </summary>
public enum GuildAuditCategory
{
    /// <summary>Actions about the guild itself (e.g. registration).</summary>
    Guild = 1,

    /// <summary>Actions about guild-level identity settings (timezone, language).</summary>
    Settings = 2,

    /// <summary>Actions about the character roster (joins, leaves, exclusions, rank changes).</summary>
    Roster = 3,

    /// <summary>Actions about members' personal availability declarations (one-off exceptions, recurring patterns).</summary>
    Availability = 4,

    /// <summary>Actions about guild branches (activation/deactivation, roster/officer role-set configuration).</summary>
    Branches = 5,

    /// <summary>Actions about raid series and raid events (creation, edits, cancellation).</summary>
    Raids = 6,
}
