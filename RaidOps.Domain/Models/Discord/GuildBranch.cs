using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// A WoW game-version branch (Retail, Classic Era, …) activated on a <see cref="Guild"/>.
/// A guild can run several branches in parallel (e.g. "TBC Anniversary" + "MoP Classic" on the
/// same Discord server), each with its own roster/officer Discord-role configuration — this
/// replaces the old guild-wide <c>RosterMode</c>/<c>MinRosterRoleId</c>/<c>MinOfficerRoleId</c>.
/// </summary>
[Table("GuildBranches")]
public class GuildBranch
{
    /// <summary>Surrogate primary key — referenced by <see cref="GuildMembership.GuildBranchId"/> and friends.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the guild this branch is activated on.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>FK to the WoW game-version branch (Retail, Classic Era, …).</summary>
    public int BranchId { get; set; }

    /// <summary>
    /// Controls who may join this branch's roster. Null until the guild owner completes the
    /// roster settings step for this branch.
    /// </summary>
    public RosterMode? RosterMode { get; set; }

    /// <summary>
    /// Discord snowflake IDs of the roles that grant roster access on this branch. Holding any
    /// one of these roles is sufficient — an explicit set, not a hierarchy-position threshold,
    /// since two branches' roles can sit at unrelated positions on the same flat Discord axis.
    /// Only relevant when <see cref="RosterMode"/> is <see cref="Enums.RosterMode.DiscordRoleOnly"/>.
    /// </summary>
    public List<string> RosterRoleIds { get; set; } = [];

    /// <summary>
    /// Discord snowflake IDs of the roles that grant Officer access on this branch. Holding any
    /// one of these roles is sufficient, independently of <see cref="RosterMode"/>. The Discord
    /// Administrator/owner safety net always applies on top, so this can never lock an admin out.
    /// </summary>
    public List<string> OfficerRoleIds { get; set; } = [];

    /// <summary>
    /// Whether this branch is currently active on the guild. Deactivating never hard-deletes the
    /// row — roster history and settings hang off this FK and are preserved for reactivation.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp of when this branch was first activated on the guild.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Blizzard API region this branch's realm sits in ("eu", "us", "kr", "tw") — determines which
    /// <see cref="WeeklyLockoutSchedule"/> row applies for this branch's weekly raid resets. Set
    /// manually by an officer (no reliable way to auto-detect it); <c>null</c> until configured,
    /// in which case region-based lockout resolution is skipped rather than guessed.
    /// </summary>
    [MaxLength(4)]
    public string? Region { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this branch is activated on.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The WoW game-version branch.</summary>
    public virtual Branch Branch { get; set; } = null!;

    /// <summary>Roster memberships on this specific guild branch.</summary>
    public virtual ICollection<GuildMembership> Memberships { get; set; } = [];
}
