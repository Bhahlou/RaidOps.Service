using RaidOps.Domain.Models.Discord;
using System.Linq.Expressions;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="User"/> aggregate persistence operations.
/// </summary>
public interface IUsersRepository
{
    /// <summary>Returns all users in the data store.</summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new user and returns the saved entity.
    /// </summary>
    /// <param name="user">The user entity to add.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies changes to an existing user and returns the updated entity.
    /// </summary>
    /// <param name="user">The user entity with updated values.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their Discord snowflake ID, or <c>null</c> if not found.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<User?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their Discord snowflake ID with their associated guilds eagerly loaded,
    /// or <c>null</c> if not found.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<User?> GetByDiscordIdWithGuildsAsync(string discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the specified user from the data store.
    /// </summary>
    /// <param name="user">The user entity to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the deletion succeeded.</returns>
    Task<bool> DeleteAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all users that satisfy the given predicate.
    /// </summary>
    /// <param name="predicate">A LINQ expression used to filter users.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<User>> FindAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default);
}
