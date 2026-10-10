using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IExpansionRepository"/>.
/// Reads the seeded <see cref="Expansion"/> reference table — no writes at runtime.
/// </summary>
public class ExpansionRepository(RaidOpsDbContext context) : IExpansionRepository
{
    /// <summary>Returns all expansions ordered by their seeded ID. Uses a no-tracking query since expansions are static reference data.</summary>
    public async Task<IEnumerable<Expansion>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Expansions
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);
}
