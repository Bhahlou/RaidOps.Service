using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="GuildBranch"/> persistence — the per-guild activation and
/// roster/officer role-set configuration of a WoW game-version branch.
/// </summary>
public interface IGuildBranchesRepository
{
    /// <summary>Returns the guild branch with the given surrogate <paramref name="id"/>, or <c>null</c> if it does not exist.</summary>
    Task<GuildBranch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the guild branch matching <paramref name="guildId"/> and <paramref name="branchId"/>
    /// (active or deactivated), or <c>null</c> if this branch has never been activated on the guild.
    /// </summary>
    Task<GuildBranch?> GetByGuildAndBranchAsync(string guildId, int branchId, CancellationToken cancellationToken = default);

    /// <summary>Returns every guild branch (active and deactivated) activated on the given guild.</summary>
    Task<List<GuildBranch>> GetAllForGuildAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>Returns only the currently active guild branches for the given guild.</summary>
    Task<List<GuildBranch>> GetActiveForGuildAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates <paramref name="branchId"/> on <paramref name="guildId"/>: creates a new
    /// <see cref="GuildBranch"/> row if this branch has never been activated before, or flips
    /// <see cref="GuildBranch.IsActive"/> back to <c>true</c> on the existing row (preserving its
    /// prior roster/officer role-set configuration) if it was previously deactivated.
    /// </summary>
    Task<GuildBranch> ActivateAsync(string guildId, int branchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the given guild branch. Never hard-deletes — roster history and role-set
    /// configuration are preserved for a future reactivation.
    /// </summary>
    /// <returns><c>true</c> if the branch existed and was deactivated; <c>false</c> if not found.</returns>
    Task<bool> DeactivateAsync(int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the roster/officer role-set configuration for the given guild branch.
    /// </summary>
    /// <returns><c>true</c> if the branch existed and was updated; <c>false</c> if not found.</returns>
    Task<bool> UpdateRosterSettingsAsync(
        int guildBranchId,
        RosterMode rosterMode,
        List<string> rosterRoleIds,
        List<string> officerRoleIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the Blizzard API region for the given guild branch, used to resolve its weekly
    /// raid-lockout schedule.
    /// </summary>
    /// <returns><c>true</c> if the branch existed and was updated; <c>false</c> if not found.</returns>
    Task<bool> UpdateRegionAsync(int guildBranchId, string region, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the default signup mode for new raid events created on the given guild branch.
    /// </summary>
    /// <returns><c>true</c> if the branch existed and was updated; <c>false</c> if not found.</returns>
    Task<bool> UpdateSignupModeAsync(int guildBranchId, SignupMode signupMode, CancellationToken cancellationToken = default);
}
