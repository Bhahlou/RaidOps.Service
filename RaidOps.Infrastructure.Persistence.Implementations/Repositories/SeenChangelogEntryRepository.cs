using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISeenChangelogEntryRepository"/>.
/// </summary>
public class SeenChangelogEntryRepository(RaidOpsDbContext context) : ISeenChangelogEntryRepository
{
    /// <summary>
    /// Returns the set of changelog entry ids the given user has acknowledged.
    /// </summary>
    public async Task<HashSet<string>> GetSeenEntryIdsAsync(string userDiscordId, CancellationToken cancellationToken = default)
    {
        var seen = await context.SeenChangelogEntries
            .Where(s => s.UserDiscordId == userDiscordId)
            .Select(s => s.EntryId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. seen];
    }

    /// <summary>
    /// Records the given entries as seen, skipping any already recorded for this user.
    /// </summary>
    public async Task MarkSeenAsync(string userDiscordId, IEnumerable<string> entryIds, CancellationToken cancellationToken = default)
    {
        var ids = entryIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var alreadySeen = await context.SeenChangelogEntries
            .Where(s => s.UserDiscordId == userDiscordId && ids.Contains(s.EntryId))
            .Select(s => s.EntryId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var newEntries = ids
            .Except(alreadySeen)
            .Select(id => new SeenChangelogEntry
            {
                UserDiscordId = userDiscordId,
                EntryId = id,
                SeenAt = DateTime.UtcNow,
            });

        context.SeenChangelogEntries.AddRange(newEntries);
        await context.SaveChangesAsync(cancellationToken);
    }
}
