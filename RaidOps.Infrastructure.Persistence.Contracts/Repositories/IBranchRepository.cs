using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Read access to the WoW branch (game version) reference table, plus wago.tools sync-state writes.</summary>
public interface IBranchRepository
{
    /// <summary>Returns all branches ordered by ID.</summary>
    Task<IEnumerable<Branch>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the branch with the given ID, or <c>null</c> if not found.</summary>
    Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every active branch that has a <see cref="Branch.WagoProductCode"/> set — the set the
    /// spell-sync job polls. Excludes deactivated branches (e.g. Classic Era) even if they have one.
    /// </summary>
    Task<List<Branch>> GetActiveWagoTrackedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that <paramref name="branchId"/> has been synced up to <paramref name="buildVersion"/>,
    /// built by Blizzard at <paramref name="buildDate"/>.
    /// </summary>
    Task UpdateSyncStateAsync(int branchId, string buildVersion, DateTime buildDate, CancellationToken cancellationToken = default);
}
