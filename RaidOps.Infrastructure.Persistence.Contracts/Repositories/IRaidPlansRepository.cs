using RaidOps.Domain.Models.Raids.Plans;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="RaidPlan"/> persistence.</summary>
public interface IRaidPlansRepository
{
    /// <summary>Returns every strategy board the guild has for the given boss.</summary>
    Task<List<RaidPlan>> GetForBossAsync(string guildId, int raidBossId, CancellationToken cancellationToken = default);

    /// <summary>Returns the board identified by <paramref name="id"/>, or <c>null</c> if not found.</summary>
    Task<RaidPlan?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new board.</summary>
    Task<RaidPlan> AddAsync(RaidPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Renames the board identified by <paramref name="id"/> on <paramref name="guildId"/>. Returns <c>false</c> if no matching row exists.</summary>
    Task<bool> RenameAsync(int id, string guildId, string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes the board identified by <paramref name="id"/> on <paramref name="guildId"/>, cascading its pages and elements. Returns <c>false</c> if no matching row exists.</summary>
    Task<bool> DeleteAsync(int id, string guildId, CancellationToken cancellationToken = default);
}
