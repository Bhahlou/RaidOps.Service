using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Branches.Responses;

/// <summary>
/// DTO returned by <see cref="Queries.GetGuildBranchesQuery"/>: one WoW branch activated on a
/// guild, with its roster/officer role-set configuration.
/// </summary>
public class GuildBranchResponse
{
    /// <summary>Surrogate ID of the <see cref="Domain.Models.Discord.GuildBranch"/>.</summary>
    public required int Id { get; set; }

    /// <summary>FK to the WoW game-version branch (Retail, Classic Era, …).</summary>
    public required int BranchId { get; set; }

    /// <summary>Display name of the WoW game-version branch (e.g. "Classic Era").</summary>
    public required string BranchName { get; set; }

    /// <summary>Whether this branch is currently active on the guild.</summary>
    public bool IsActive { get; set; }

    /// <summary>Controls who may join this branch's roster, or <c>null</c> if not yet configured.</summary>
    public RosterMode? RosterMode { get; set; }

    /// <summary>Discord snowflake IDs of the roles that grant roster access on this branch.</summary>
    public List<string> RosterRoleIds { get; set; } = [];

    /// <summary>Discord snowflake IDs of the roles that grant Officer access on this branch.</summary>
    public List<string> OfficerRoleIds { get; set; } = [];
}
