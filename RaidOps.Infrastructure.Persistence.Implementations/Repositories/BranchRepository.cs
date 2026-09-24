using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBranchRepository"/>.
/// Reads the seeded <see cref="Branch"/> reference table; the only runtime write is the wago.tools
/// sync-state pair updated by the spell-sync job.
/// </summary>
public class BranchRepository(RaidOpsDbContext context) : IBranchRepository
{
    /// <summary>
    /// Returns all branches ordered by their seeded ID.
    /// Uses a no-tracking query since branches are static reference data.
    /// </summary>
    public async Task<IEnumerable<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Branches
            .AsNoTracking()
            .Include(b => b.CurrentExpansion)
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns the branch with the given ID, or <c>null</c> if not found.
    /// </summary>
    public async Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Branches
            .AsNoTracking()
            .Include(b => b.CurrentExpansion)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<Branch>> GetActiveWagoTrackedAsync(CancellationToken cancellationToken = default)
        => await context.Branches
            .AsNoTracking()
            .Include(b => b.CurrentExpansion)
            .Where(b => b.IsActive && b.WagoProductCode != null)
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task UpdateSyncStateAsync(int branchId, string buildVersion, DateTime buildDate, CancellationToken cancellationToken = default)
    {
        var branch = await context.Branches.FirstAsync(b => b.Id == branchId, cancellationToken);
        branch.LastSyncedBuildVersion = buildVersion;
        branch.LastSyncedBuildDate = buildDate;
        await context.SaveChangesAsync(cancellationToken);
    }
}
