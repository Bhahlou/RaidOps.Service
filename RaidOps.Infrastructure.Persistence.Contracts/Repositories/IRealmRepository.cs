using RaidOps.Domain.Models.Character;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Persistence for WoW realms.
/// Realms are not pre-populated — they are cached on-demand when characters are imported.
/// </summary>
public interface IRealmRepository
{
    /// <summary>
    /// Returns the realm matching <paramref name="slug"/> and <paramref name="branchId"/>,
    /// or <c>null</c> if it has not been cached yet.
    /// </summary>
    Task<Realm?> GetBySlugAndBranchAsync(string slug, int branchId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new realm and returns the entity with its generated ID.</summary>
    Task<Realm> AddAsync(Realm realm, CancellationToken cancellationToken = default);
}
