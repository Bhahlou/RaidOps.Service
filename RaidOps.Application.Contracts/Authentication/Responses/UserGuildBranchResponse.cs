using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Authentication.Responses;

/// <summary>
/// Lightweight representation of one active WoW branch on a guild the authenticated user belongs
/// to, including their access level on that specific branch.
/// </summary>
public class UserGuildBranchResponse
{
    /// <summary>Surrogate ID of the <see cref="RaidOps.Domain.Models.Discord.GuildBranch"/>.</summary>
    public required int Id { get; set; }

    /// <summary>FK to the WoW game-version branch (Retail, Classic Era, …).</summary>
    public required int BranchId { get; set; }

    /// <summary>Display name of the WoW game-version branch (e.g. "Classic Era").</summary>
    public required string BranchName { get; set; }

    /// <summary>The user's access level on this specific branch (Public/Roster/Officer).</summary>
    public GuildAccessLevel AccessLevel { get; set; }

    /// <summary>
    /// Whether the user has at least one active roster character on this branch. Unlike
    /// <see cref="AccessLevel"/> (derived from Discord roles), this reflects an actual
    /// <see cref="RaidOps.Domain.Models.Discord.GuildMembership"/> row — a branch can grant
    /// Roster-level Discord access without the user ever having registered a character on it.
    /// </summary>
    public bool HasActiveCharacter { get; set; }
}
