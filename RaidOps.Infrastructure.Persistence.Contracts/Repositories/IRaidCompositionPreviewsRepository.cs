using RaidOps.Domain.Models.Raids.CompositionPreviews;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for <see cref="RaidCompositionPreview"/> persistence, including its sparse slot grid.</summary>
public interface IRaidCompositionPreviewsRepository
{
    /// <summary>Returns every preview belonging to the guild branch, without slot detail — backs the list page.</summary>
    Task<List<RaidCompositionPreview>> GetForGuildBranchAsync(int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Returns a single preview with its full slot grid (class/spec/note), or <c>null</c> if it doesn't belong to the guild branch.</summary>
    Task<RaidCompositionPreview?> GetByIdAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new preview, including any slots already attached to it (used by duplication).</summary>
    Task<RaidCompositionPreview> AddAsync(RaidCompositionPreview preview, CancellationToken cancellationToken = default);

    /// <summary>Renames a preview and bumps its <see cref="RaidCompositionPreview.UpdatedAt"/>. Returns <c>false</c> if not found.</summary>
    Task<bool> RenameAsync(int id, int guildBranchId, string name, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a preview, including its slots. Returns <c>false</c> if not found.</summary>
    Task<bool> DeleteAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the slot at <paramref name="slot"/>'s (group, slot) coordinate — atomically removes
    /// any existing row at that coordinate first, then inserts the new one. Also bumps the parent
    /// preview's <see cref="RaidCompositionPreview.UpdatedAt"/>.
    /// </summary>
    Task UpsertSlotAsync(RaidCompositionPreviewSlot slot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the slot row at the given coordinate, if any, and bumps the parent preview's
    /// <see cref="RaidCompositionPreview.UpdatedAt"/>. Returns <c>false</c> if the slot was already empty.
    /// </summary>
    Task<bool> ClearSlotAsync(int previewId, int groupNumber, int slotNumber, CancellationToken cancellationToken = default);
}
