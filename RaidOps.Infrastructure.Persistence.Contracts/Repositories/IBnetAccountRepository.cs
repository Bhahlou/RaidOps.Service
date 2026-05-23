using RaidOps.Domain.Models.Character;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="BattleNetAccount"/> persistence operations.
/// </summary>
public interface IBnetAccountRepository
{
    /// <summary>
    /// Inserts or updates the Battle.net account linked to a user.
    /// Matches on <see cref="BattleNetAccount.UserDiscordId"/> (primary key).
    /// </summary>
    /// <param name="account">The account to insert or update.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UpsertAsync(BattleNetAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Battle.net account linked to the specified user, or <c>null</c> if not linked.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<BattleNetAccount?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken = default);
}
