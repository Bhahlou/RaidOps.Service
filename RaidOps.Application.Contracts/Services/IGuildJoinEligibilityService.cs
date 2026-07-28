using RaidOps.Application.Contracts.Common;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Resolves whether a character can join a guild's roster on the branch matching its own WoW
/// version, and returns that branch when it can — the single place this eligibility rule
/// (branch active, roster configured, Discord role access) lives, instead of duplicated inline
/// in <c>JoinGuildCommandHandler</c>.
/// </summary>
public interface IGuildJoinEligibilityService
{
    /// <summary>
    /// Returns the active <see cref="GuildBranch"/> matching <paramref name="characterBranchId"/>
    /// on <paramref name="guildId"/> when <paramref name="requesterDiscordId"/> is allowed to join
    /// its roster, or a failed <see cref="Result{T}"/> with the specific reason otherwise
    /// (branch not run by this guild, roster not configured yet, or insufficient Discord role
    /// access).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="characterBranchId">The WoW branch ID of the character trying to join.</param>
    /// <param name="requesterDiscordId">The Discord snowflake ID of the requester.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<Result<GuildBranch>> ResolveEligibleBranchAsync(
        string guildId,
        int characterBranchId,
        string requesterDiscordId,
        CancellationToken cancellationToken = default);
}
