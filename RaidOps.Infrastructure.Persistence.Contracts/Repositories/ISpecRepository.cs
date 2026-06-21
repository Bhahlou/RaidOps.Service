using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Read-only access to the static <see cref="Spec"/> reference table.</summary>
public interface ISpecRepository
{
    /// <summary>Returns all specs ordered by Blizzard spec ID, including their class.</summary>
    Task<IEnumerable<Spec>> GetAllAsync(CancellationToken cancellationToken = default);
}
