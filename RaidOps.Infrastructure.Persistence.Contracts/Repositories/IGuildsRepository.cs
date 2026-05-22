using RaidOps.Domain.Models;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="Guild"/> master-data persistence.
/// </summary>
public interface IGuildsRepository
{
    /// <summary>
    /// Inserts new guilds or updates the name and icon hash of guilds that already exist,
    /// matching on <see cref="Guild.Id"/>.
    /// </summary>
    /// <param name="guilds">The collection of guilds to upsert.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UpsertRangeAsync(IEnumerable<Guild> guilds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the specified guild as registered in RaidOps by setting <see cref="Guild.IsRegistered"/> to <c>true</c>.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to register.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the guild was found and updated; <c>false</c> if no matching guild exists.</returns>
    Task<bool> RegisterAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the specified guild as unregistered in RaidOps by setting <see cref="Guild.IsRegistered"/> to <c>false</c>.
    /// Silently no-ops if the guild does not exist (idempotent).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to unregister.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UnregisterAsync(string guildId, CancellationToken cancellationToken = default);
}
