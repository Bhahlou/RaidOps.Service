namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Resolves the set of guild branches a player should be considered part of for fan-out purposes
/// (Global availability notifications, Phase D's <c>HasActiveCharacter</c>) — every
/// <c>(GuildId, GuildBranchId)</c> pair where the player has at least one character with
/// <c>IsActiveInRaidOps == true</c> on that branch's roster.
/// </summary>
public interface IActiveRosterBranchResolver
{
    /// <summary>
    /// Returns every branch the given player has an active roster character on, deduplicated.
    /// </summary>
    /// <param name="userDiscordId">Discord snowflake ID of the player.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<ActiveRosterBranch>> GetActiveBranchesAsync(string userDiscordId, CancellationToken cancellationToken = default);
}

/// <summary>One guild branch a player has an active roster character on.</summary>
/// <param name="GuildId">Discord snowflake ID of the guild.</param>
/// <param name="GuildBranchId">Surrogate ID of the guild branch.</param>
public record ActiveRosterBranch(string GuildId, int GuildBranchId);
