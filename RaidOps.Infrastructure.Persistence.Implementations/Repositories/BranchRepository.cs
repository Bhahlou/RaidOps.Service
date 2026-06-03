using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBranchRepository"/>.
/// Reads the seeded <see cref="Branch"/> reference table — no writes at runtime.
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
}
