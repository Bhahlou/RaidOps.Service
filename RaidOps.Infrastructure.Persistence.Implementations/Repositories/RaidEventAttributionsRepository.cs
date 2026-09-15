using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IRaidEventAttributionsRepository"/>.</summary>
public class RaidEventAttributionsRepository(RaidOpsDbContext context) : IRaidEventAttributionsRepository
{
    /// <inheritdoc/>
    public async Task<List<RaidEventAttribution>> GetForEventAsync(int raidEventId, CancellationToken cancellationToken = default)
        => await context.RaidEventAttributions
            .Where(a => a.RaidEventId == raidEventId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task SetAsync(int raidEventId, int cellId, int definitionId, int instanceIndex, int characterId, string assignedByDiscordId, CancellationToken cancellationToken = default)
    {
        var existing = await context.RaidEventAttributions
            .FirstOrDefaultAsync(a => a.RaidEventId == raidEventId && a.AttributionDefinitionCellId == cellId && a.InstanceIndex == instanceIndex, cancellationToken);

        if (existing != null)
        {
            existing.CharacterId = characterId;
            existing.AssignedAt = DateTime.UtcNow;
            existing.AssignedByDiscordId = assignedByDiscordId;
        }
        else
        {
            context.RaidEventAttributions.Add(new RaidEventAttribution
            {
                RaidEventId = raidEventId,
                AttributionDefinitionCellId = cellId,
                GuildAttributionDefinitionId = definitionId,
                InstanceIndex = instanceIndex,
                CharacterId = characterId,
                AssignedAt = DateTime.UtcNow,
                AssignedByDiscordId = assignedByDiscordId,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ClearAsync(int raidEventId, int cellId, int instanceIndex, CancellationToken cancellationToken = default)
    {
        var deleted = await context.RaidEventAttributions
            .Where(a => a.RaidEventId == raidEventId && a.AttributionDefinitionCellId == cellId && a.InstanceIndex == instanceIndex)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }
}
