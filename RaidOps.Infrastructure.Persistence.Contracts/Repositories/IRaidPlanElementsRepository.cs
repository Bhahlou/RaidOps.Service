using RaidOps.Domain.Models.Raids.Plans;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for bulk-saving a <see cref="RaidPlanPage"/>'s <see cref="RaidPlanElement"/>s.</summary>
public interface IRaidPlanElementsRepository
{
    /// <summary>
    /// Replaces every element of <paramref name="raidPlanPageId"/> with <paramref name="elements"/> in
    /// one transaction: rows whose <see cref="RaidPlanElement.Id"/> is not among the page's current
    /// elements are inserted, rows matching an existing <see cref="RaidPlanElement.Id"/> are updated,
    /// and existing rows absent from <paramref name="elements"/> are deleted. This is the one bulk
    /// mutation the editor's Save button fires — there is no per-element CRUD.
    /// </summary>
    Task SaveAsync(int raidPlanPageId, List<RaidPlanElement> elements, CancellationToken cancellationToken = default);
}
