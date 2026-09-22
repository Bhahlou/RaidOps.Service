using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids.Plans;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IRaidPlanPagesRepository"/>.</summary>
public class RaidPlanPagesRepository(RaidOpsDbContext context) : IRaidPlanPagesRepository
{
    /// <inheritdoc/>
    public async Task<List<RaidPlanPage>> GetForPlanAsync(int raidPlanId, CancellationToken cancellationToken = default)
        => await context.RaidPlanPages
            .Where(p => p.RaidPlanId == raidPlanId)
            .OrderBy(p => p.SortOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidPlanPage?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.RaidPlanPages
            .Include(p => p.RaidPlan)
            .Include(p => p.Elements.OrderBy(e => e.ZIndex)).ThenInclude(e => e.Spell)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidPlanPage> AddAsync(RaidPlanPage page, CancellationToken cancellationToken = default)
    {
        // Densely renumbered per-board, same convention as GuildAttributionDefinitionsRepository.AddAsync.
        var siblingCount = await context.RaidPlanPages
            .Where(p => p.RaidPlanId == page.RaidPlanId)
            .CountAsync(cancellationToken);
        page.SortOrder = siblingCount;

        context.RaidPlanPages.Add(page);
        await context.SaveChangesAsync(cancellationToken);
        return page;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(int id, int raidPlanId, string name, CancellationToken cancellationToken = default)
    {
        var updated = await context.RaidPlanPages
            .Where(p => p.Id == id && p.RaidPlanId == raidPlanId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, name), cancellationToken);
        return updated > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id, int raidPlanId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.RaidPlanPages
            .Where(p => p.Id == id && p.RaidPlanId == raidPlanId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    /// <inheritdoc/>
    public async Task ReorderAsync(int raidPlanId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        var pages = await context.RaidPlanPages
            .Where(p => p.RaidPlanId == raidPlanId && orderedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (pages.TryGetValue(orderedIds[i], out var page))
                page.SortOrder = i;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SetBackgroundAsync(int id, int raidPlanId, string? backgroundImageKey, CancellationToken cancellationToken = default)
    {
        var updated = await context.RaidPlanPages
            .Where(p => p.Id == id && p.RaidPlanId == raidPlanId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.BackgroundImageKey, backgroundImageKey), cancellationToken);
        return updated > 0;
    }
}
