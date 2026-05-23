using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for managing the many-to-many relationship between users and Discord guilds.
/// </summary>
public interface IUserGuildsRepository
{
    /// <summary>
    /// Returns all <see cref="UserGuild"/> records associated with the specified user.
    /// </summary>
    /// <param name="userDiscordId">The Discord snowflake ID of the user whose guild memberships to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<List<UserGuild>> GetByUserDiscordIdAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replaces all guild memberships for a user with the supplied collection.
    /// Existing records are deleted before the new ones are inserted.
    /// </summary>
    /// <param name="userDiscordId">The Discord snowflake ID of the user whose guilds are being replaced.</param>
    /// <param name="guilds">The new set of <see cref="UserGuild"/> records to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task ReplaceUserGuildsAsync(string userDiscordId, IEnumerable<UserGuild> guilds, CancellationToken cancellationToken = default);
}
