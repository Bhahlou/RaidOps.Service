using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Read-only access to the static <see cref="Expansion"/> reference table.</summary>
public interface IExpansionRepository
{
    /// <summary>Returns all expansions ordered by ID.</summary>
    Task<IEnumerable<Expansion>> GetAllAsync(CancellationToken cancellationToken = default);
}
