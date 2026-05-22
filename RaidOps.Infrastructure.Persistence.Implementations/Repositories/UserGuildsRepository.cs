using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserGuildsRepository"/>.
/// Manages the <see cref="UserGuild"/> join table that tracks which Discord guilds
/// a user belongs to.
/// </summary>
public class UserGuildsRepository(RaidOpsDbContext context) : IUserGuildsRepository
{
    /// <summary>
    /// Returns all <see cref="UserGuild"/> records associated with the specified user.
    /// </summary>
    /// <param name="userDiscordId">The Discord snowflake ID of the user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task<List<UserGuild>> GetByUserDiscordIdAsync(string userDiscordId, CancellationToken cancellationToken = default)
        => await context.UserGuilds
            .Where(ug => ug.UserDiscordId == userDiscordId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Atomically replaces all guild memberships for a user: deletes the current set
    /// and inserts the provided collection in a single <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="userDiscordId">The Discord snowflake ID of the user whose guilds are being replaced.</param>
    /// <param name="guilds">The new set of guild memberships to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task ReplaceUserGuildsAsync(string userDiscordId, IEnumerable<UserGuild> guilds, CancellationToken cancellationToken = default)
    {
        var existing = await context.UserGuilds
            .Where(ug => ug.UserDiscordId == userDiscordId)
            .ToListAsync(cancellationToken);

        context.UserGuilds.RemoveRange(existing);
        await context.UserGuilds.AddRangeAsync(guilds, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
