using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISeenTourRepository"/>.
/// </summary>
public class SeenTourRepository(RaidOpsDbContext context) : ISeenTourRepository
{
    /// <summary>
    /// Returns the set of tour ids the given user has already seen.
    /// </summary>
    public async Task<HashSet<string>> GetSeenTourIdsAsync(string userDiscordId, CancellationToken cancellationToken = default)
    {
        var seen = await context.SeenTours
            .Where(s => s.UserDiscordId == userDiscordId)
            .Select(s => s.TourId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. seen];
    }

    /// <summary>
    /// Records the given tour as seen, doing nothing if it is already recorded for this user.
    /// </summary>
    public async Task MarkSeenAsync(string userDiscordId, string tourId, CancellationToken cancellationToken = default)
    {
        var alreadySeen = await context.SeenTours
            .AnyAsync(s => s.UserDiscordId == userDiscordId && s.TourId == tourId, cancellationToken);
        if (alreadySeen)
            return;

        context.SeenTours.Add(new SeenTour
        {
            UserDiscordId = userDiscordId,
            TourId = tourId,
            SeenAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
