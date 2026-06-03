using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Read access to the WoW branch (game version) reference table.</summary>
public interface IBranchRepository
{
    /// <summary>Returns all branches ordered by ID.</summary>
    Task<IEnumerable<Branch>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the branch with the given ID, or <c>null</c> if not found.</summary>
    Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
