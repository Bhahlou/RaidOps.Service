using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids.Plans;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IRaidPlansRepository"/>.</summary>
public class RaidPlansRepository(RaidOpsDbContext context) : IRaidPlansRepository
{
    /// <inheritdoc/>
    public async Task<List<RaidPlan>> GetForBossAsync(string guildId, int raidBossId, CancellationToken cancellationToken = default)
        => await context.RaidPlans
            .Where(p => p.GuildId == guildId && p.RaidBossId == raidBossId)
            .OrderBy(p => p.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidPlan?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.RaidPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidPlan> AddAsync(RaidPlan plan, CancellationToken cancellationToken = default)
    {
        context.RaidPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return plan;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(int id, string guildId, string name, CancellationToken cancellationToken = default)
    {
        var updated = await context.RaidPlans
            .Where(p => p.Id == id && p.GuildId == guildId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, name), cancellationToken);
        return updated > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id, string guildId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.RaidPlans
            .Where(p => p.Id == id && p.GuildId == guildId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }
}
