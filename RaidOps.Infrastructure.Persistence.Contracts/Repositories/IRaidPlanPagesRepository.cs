using RaidOps.Domain.Models.Raids.Plans;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="RaidPlanPage"/> persistence.</summary>
public interface IRaidPlanPagesRepository
{
    /// <summary>Returns every page of the given board, ordered by <see cref="RaidPlanPage.SortOrder"/>. Does not include elements.</summary>
    Task<List<RaidPlanPage>> GetForPlanAsync(int raidPlanId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the page identified by <paramref name="id"/>, including its board (for guild-ownership
    /// checks) and its elements ordered by <see cref="RaidPlanElement.ZIndex"/>, or <c>null</c> if not found.
    /// </summary>
    Task<RaidPlanPage?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new page, appended after the board's current last page.</summary>
    Task<RaidPlanPage> AddAsync(RaidPlanPage page, CancellationToken cancellationToken = default);

    /// <summary>Renames the page identified by <paramref name="id"/> on <paramref name="raidPlanId"/>. Returns <c>false</c> if no matching row exists.</summary>
    Task<bool> RenameAsync(int id, int raidPlanId, string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes the page identified by <paramref name="id"/> on <paramref name="raidPlanId"/>, cascading its elements. Returns <c>false</c> if no matching row exists.</summary>
    Task<bool> DeleteAsync(int id, int raidPlanId, CancellationToken cancellationToken = default);

    /// <summary>Re-numbers <see cref="RaidPlanPage.SortOrder"/> for the board's pages to match <paramref name="orderedIds"/>'s order. IDs not belonging to the board are ignored.</summary>
    Task ReorderAsync(int raidPlanId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default);

    /// <summary>Sets the background image key of the page identified by <paramref name="id"/> on <paramref name="raidPlanId"/>. Returns <c>false</c> if no matching row exists.</summary>
    Task<bool> SetBackgroundAsync(int id, int raidPlanId, string? backgroundImageKey, CancellationToken cancellationToken = default);
}
