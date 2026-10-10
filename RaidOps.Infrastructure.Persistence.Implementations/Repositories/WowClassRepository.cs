using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IWowClassRepository"/>.
/// Reads the seeded <see cref="WowClass"/> reference table — no writes at runtime.
/// </summary>
public class WowClassRepository(RaidOpsDbContext context) : IWowClassRepository
{
    /// <summary>Returns all classes ordered by their seeded Blizzard ID. Uses a no-tracking query since classes are static reference data.</summary>
    public async Task<IEnumerable<WowClass>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.WowClasses
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
}
