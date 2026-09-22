using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids.Plans;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IRaidPlanElementsRepository"/>.</summary>
public class RaidPlanElementsRepository(RaidOpsDbContext context) : IRaidPlanElementsRepository
{
    /// <inheritdoc/>
    public async Task SaveAsync(int raidPlanPageId, List<RaidPlanElement> elements, CancellationToken cancellationToken = default)
    {
        var existing = await context.RaidPlanElements
            .Where(e => e.RaidPlanPageId == raidPlanPageId)
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var keptIds = new HashSet<int>();

        foreach (var element in elements)
        {
            element.RaidPlanPageId = raidPlanPageId;

            if (element.Id != 0 && existing.TryGetValue(element.Id, out var current))
            {
                context.Entry(current).CurrentValues.SetValues(element);
                keptIds.Add(element.Id);
            }
            else
            {
                element.Id = 0;
                context.RaidPlanElements.Add(element);
            }
        }

        var removedIds = existing.Keys.Except(keptIds).ToList();
        if (removedIds.Count > 0)
        {
            await context.RaidPlanElements
                .Where(e => removedIds.Contains(e.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
