using RaidOps.Domain.Models.Character;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="BattleNetAccount"/> persistence operations.
/// </summary>
public interface IBnetAccountRepository
{
    /// <summary>
    /// Inserts or updates a Battle.net account linked to a user.
    /// Matches on the composite key (<see cref="BattleNetAccount.UserDiscordId"/>,
    /// <see cref="BattleNetAccount.BnetId"/>) — a new <c>BnetId</c> for the same user inserts an
    /// additional linked account rather than replacing the existing one.
    /// </summary>
    /// <param name="account">The account to insert or update.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UpsertAsync(BattleNetAccount account, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all Battle.net accounts linked to the specified user, or an empty list if none.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<BattleNetAccount>> GetAllByDiscordIdAsync(string discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the Battle.net account matching the given user and BNet ID, if it exists.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID of the user.</param>
    /// <param name="bnetId">The Blizzard account ID of the linked account to remove.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task DeleteAsync(string discordId, string bnetId, CancellationToken cancellationToken = default);
}
