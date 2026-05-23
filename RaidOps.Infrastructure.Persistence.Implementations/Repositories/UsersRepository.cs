using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUsersRepository"/>.
/// Inherits generic CRUD operations from <see cref="BaseRepository{TEntity}"/>
/// and adds a Discord-ID–specific lookup.
/// </summary>
public class UsersRepository(RaidOpsDbContext dbContext) : BaseRepository<User>(dbContext), IUsersRepository
{
    /// <summary>
    /// Retrieves a user by their Discord snowflake ID, or <c>null</c> if no matching record exists.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task<User?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken = default)
        => await _dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId, cancellationToken);

    /// <summary>
    /// Retrieves a user by their Discord snowflake ID with their associated guilds eagerly loaded,
    /// or <c>null</c> if no matching record exists.
    /// </summary>
    /// <param name="discordId">The Discord snowflake ID to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task<User?> GetByDiscordIdWithGuildsAsync(string discordId, CancellationToken cancellationToken = default)
        => await _dbContext.Users
            .Include(u => u.UserGuilds)
            .ThenInclude(ug => ug.Guild)
            .FirstOrDefaultAsync(u => u.DiscordId == discordId, cancellationToken);
}
