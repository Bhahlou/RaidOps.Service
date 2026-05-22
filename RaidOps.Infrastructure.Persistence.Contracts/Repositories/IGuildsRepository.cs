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
}
