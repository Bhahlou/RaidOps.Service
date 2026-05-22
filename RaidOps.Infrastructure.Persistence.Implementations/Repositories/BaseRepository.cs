using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// Generic repository base class providing common CRUD and query operations
/// backed by an Entity Framework Core <see cref="RaidOpsDbContext"/>.
/// Concrete repositories inherit from this class and may override any virtual member.
/// </summary>
/// <typeparam name="TEntity">The domain entity type managed by this repository.</typeparam>
public abstract class BaseRepository<TEntity>(RaidOpsDbContext dbContext) where TEntity : class, new()
{
    /// <summary>The underlying EF Core database context.</summary>
    protected readonly RaidOpsDbContext _dbContext = dbContext;

    /// <summary>
    /// Retrieves an entity by its integer primary key, or <c>null</c> if not found.
    /// Uses a no-tracking query to avoid change-tracker overhead.
    /// </summary>
    /// <param name="id">The integer primary key value.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public virtual async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, cancellationToken);

    /// <summary>
    /// Returns all entities of type <typeparamref name="TEntity"/> as a no-tracking list.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public virtual async Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>
    /// Persists a new entity and saves changes. If the entity is already attached to the
    /// change tracker it is re-attached rather than re-inserted.
    /// </summary>
    /// <param name="t">The entity to add.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The saved entity.</returns>
    public virtual async Task<TEntity> AddAsync(TEntity t, CancellationToken cancellationToken = default)
    {
        var entry = _dbContext.Entry(t);
        if (entry.State == EntityState.Detached)
            await _dbContext.Set<TEntity>().AddAsync(t, cancellationToken);
        else
            _dbContext.Attach(t);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return t;
    }

    /// <summary>
    /// Marks an existing entity as modified, saves changes, and clears the change tracker
    /// to prevent stale state on subsequent operations.
    /// </summary>
    /// <param name="t">The entity with updated values.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The updated entity.</returns>
    public virtual async Task<TEntity> UpdateAsync(TEntity t, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Update(t);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return t;
    }

    /// <summary>
    /// Returns all entities that satisfy the given predicate as a no-tracking list.
    /// </summary>
    /// <param name="match">A LINQ expression used to filter entities.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public virtual async Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> match, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().Where(match).AsNoTracking().ToListAsync(cancellationToken);

    /// <summary>
    /// Returns the single entity that satisfies the given predicate, or <c>null</c> if none match.
    /// Throws if more than one entity matches.
    /// </summary>
    /// <param name="match">A LINQ expression used to identify the entity.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public virtual async Task<TEntity?> FindOneAsync(Expression<Func<TEntity, bool>> match, CancellationToken cancellationToken = default)
        => await _dbContext.Set<TEntity>().SingleOrDefaultAsync(match, cancellationToken);

    /// <summary>
    /// Returns the number of entities that satisfy an optional filter predicate.
    /// When no filter is supplied, counts all entities of type <typeparamref name="TEntity"/>.
    /// </summary>
    /// <param name="filter">An optional predicate to restrict the count; pass <c>null</c> to count all.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    public async Task<int> CountAsync(Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default)
        => filter != null
            ? await _dbContext.Set<TEntity>().CountAsync(filter, cancellationToken)
            : await _dbContext.Set<TEntity>().CountAsync(cancellationToken);

    /// <summary>
    /// Removes the specified entity from the data store and saves changes.
    /// </summary>
    /// <param name="t">The entity to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the deletion succeeded.</returns>
    public virtual async Task<bool> DeleteAsync(TEntity t, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<TEntity>().Remove(t);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
