using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Discord;
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

    /// <summary>
    /// Sets <see cref="Guild.IsRegistered"/> to <c>true</c> for the guild identified by <paramref name="guildId"/>.
    /// Does nothing if the guild does not exist in the database.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to register.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the guild was found and updated; <c>false</c> otherwise.</returns>
    public async Task<bool> RegisterAsync(string guildId, CancellationToken cancellationToken = default)
    {
        var guild = await context.Guilds.FindAsync([guildId], cancellationToken);
        if (guild == null) return false;

        guild.IsRegistered = true;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Sets <see cref="Guild.IsRegistered"/> to <c>false</c> for the guild identified by <paramref name="guildId"/>.
    /// Silently no-ops if the guild does not exist.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to unregister.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task UnregisterAsync(string guildId, CancellationToken cancellationToken = default)
    {
        var guild = await context.Guilds.FindAsync([guildId], cancellationToken);
        if (guild == null) return;

        guild.IsRegistered = false;
        await context.SaveChangesAsync(cancellationToken);
    }
}
