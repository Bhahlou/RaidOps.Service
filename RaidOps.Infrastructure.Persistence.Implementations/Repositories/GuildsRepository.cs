using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IGuildsRepository"/>.
/// Handles upsert logic for Discord guild master data without a base-class dependency,
/// because guilds are never deleted via this path.
/// </summary>
public class GuildsRepository(RaidOpsDbContext context) : IGuildsRepository
{
    /// <summary>
    /// Inserts guilds that do not yet exist in the database and updates the
    /// <see cref="Guild.Name"/> and <see cref="Guild.IconHash"/> of those that do,
    /// matching records by <see cref="Guild.Id"/>.
    /// </summary>
    /// <param name="guilds">The guilds to insert or update.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task UpsertRangeAsync(IEnumerable<Guild> guilds, CancellationToken cancellationToken = default)
    {
        var guildList = guilds.ToList();
        var ids = guildList.Select(g => g.Id).ToList();

        var existing = await context.Guilds
            .Where(g => ids.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        foreach (var guild in guildList)
        {
            if (existing.TryGetValue(guild.Id, out var existingGuild))
            {
                existingGuild.Name = guild.Name;
                existingGuild.IconHash = guild.IconHash;
            }
            else
            {
                context.Guilds.Add(guild);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
