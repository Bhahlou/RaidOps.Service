using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISpecRepository"/>.
/// Reads the seeded <see cref="Spec"/> reference table — no writes at runtime.
/// </summary>
public class SpecRepository(RaidOpsDbContext context) : ISpecRepository
{
    /// <summary>
    /// Returns all specs ordered by their seeded Blizzard ID.
    /// Uses a no-tracking query since specs are static reference data.
    /// </summary>
    public async Task<IEnumerable<Spec>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Specs
            .AsNoTracking()
            .Include(s => s.Class)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);
}
