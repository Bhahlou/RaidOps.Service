using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IGuildAttributionDefinitionsRepository"/>.</summary>
public class GuildAttributionDefinitionsRepository(RaidOpsDbContext context) : IGuildAttributionDefinitionsRepository
{
    /// <inheritdoc/>
    public async Task<List<GuildAttributionDefinition>> GetForGuildAsync(string guildId, CancellationToken cancellationToken = default)
        => await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId)
            .Include(d => d.Cells.OrderBy(c => c.CellIndex)).ThenInclude(c => c.Spell)
            .OrderBy(d => d.SortOrder)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildAttributionDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.GuildAttributionDefinitions
            .Include(d => d.Cells.OrderBy(c => c.CellIndex)).ThenInclude(c => c.Spell)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildAttributionDefinition> AddAsync(GuildAttributionDefinition definition, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == definition.GuildId)
            .Select(d => (int?)d.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        definition.SortOrder = maxSortOrder + 1;

        context.GuildAttributionDefinitions.Add(definition);
        await context.SaveChangesAsync(cancellationToken);
        return definition;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(GuildAttributionDefinition definition, string guildId, CancellationToken cancellationToken = default)
    {
        var existing = await context.GuildAttributionDefinitions
            .Include(d => d.Cells)
            .FirstOrDefaultAsync(d => d.Id == definition.Id && d.GuildId == guildId, cancellationToken);
        if (existing == null)
            return false;

        existing.Label = definition.Label;
        existing.Section = definition.Section;
        existing.IsRepeatable = definition.IsRepeatable;

        existing.Cells.Clear();
        foreach (var cell in definition.Cells)
            existing.Cells.Add(cell);

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id, string guildId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.GuildAttributionDefinitions
            .Where(d => d.Id == id && d.GuildId == guildId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    /// <inheritdoc/>
    public async Task ReorderAsync(string guildId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        var definitions = await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId && orderedIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (definitions.TryGetValue(orderedIds[i], out var definition))
                definition.SortOrder = i;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
