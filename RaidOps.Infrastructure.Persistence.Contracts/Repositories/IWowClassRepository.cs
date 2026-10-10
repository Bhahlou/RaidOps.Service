using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Read-only access to the static <see cref="WowClass"/> reference table.</summary>
public interface IWowClassRepository
{
    /// <summary>Returns all classes ordered by Blizzard class ID.</summary>
    Task<IEnumerable<WowClass>> GetAllAsync(CancellationToken cancellationToken = default);
}
