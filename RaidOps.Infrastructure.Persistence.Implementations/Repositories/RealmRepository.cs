using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRealmRepository"/>.
/// Realms are cached on-demand when characters are imported.
/// </summary>
public class RealmRepository(RaidOpsDbContext context) : IRealmRepository
{
    /// <summary>
    /// Returns the realm matching the given slug and branch ID, or <c>null</c> if not yet cached.
    /// Uses a no-tracking query since realms are looked up frequently and never mutated after creation.
    /// </summary>
    public async Task<Realm?> GetBySlugAndBranchAsync(string slug, int branchId, CancellationToken cancellationToken = default)
        => await context.Realms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Slug == slug && r.BranchId == branchId, cancellationToken);

    /// <summary>
    /// Inserts a new realm and returns the entity with its DB-generated ID.
    /// </summary>
    public async Task<Realm> AddAsync(Realm realm, CancellationToken cancellationToken = default)
    {
        context.Realms.Add(realm);
        await context.SaveChangesAsync(cancellationToken);
        return realm;
    }
}
