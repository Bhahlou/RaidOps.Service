using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRaidCompositionPreviewsRepository"/>. Slot upserts are
/// implemented directly against the context, without a base class, since a slot's surrogate PK
/// means "set to null/null/null" is a delete rather than an update with null values — same
/// replace-not-add shape as <see cref="RaidCompositionRepository.AssignCharacterAsync"/>.
/// </summary>
public class RaidCompositionPreviewsRepository(RaidOpsDbContext context) : IRaidCompositionPreviewsRepository
{
    /// <inheritdoc/>
    public async Task<List<RaidCompositionPreview>> GetForGuildBranchAsync(int guildBranchId, CancellationToken cancellationToken = default)
        => await context.RaidCompositionPreviews
            .Where(p => p.GuildBranchId == guildBranchId)
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidCompositionPreview?> GetByIdAsync(int id, int guildBranchId, CancellationToken cancellationToken = default)
        => await context.RaidCompositionPreviews
            .Where(p => p.Id == id && p.GuildBranchId == guildBranchId)
            .Include(p => p.Slots).ThenInclude(s => s.WowClass)
            .Include(p => p.Slots).ThenInclude(s => s.Spec)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidCompositionPreview> AddAsync(RaidCompositionPreview preview, CancellationToken cancellationToken = default)
    {
        context.RaidCompositionPreviews.Add(preview);
        await context.SaveChangesAsync(cancellationToken);
        return preview;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(int id, int guildBranchId, string name, CancellationToken cancellationToken = default)
    {
        var count = await context.RaidCompositionPreviews
            .Where(p => p.Id == id && p.GuildBranchId == guildBranchId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Name, name)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), cancellationToken);
        return count > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var count = await context.RaidCompositionPreviews
            .Where(p => p.Id == id && p.GuildBranchId == guildBranchId)
            .ExecuteDeleteAsync(cancellationToken);
        return count > 0;
    }

    /// <inheritdoc/>
    public async Task UpsertSlotAsync(RaidCompositionPreviewSlot slot, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.RaidCompositionPreviewSlots
            .Where(s => s.RaidCompositionPreviewId == slot.RaidCompositionPreviewId && s.GroupNumber == slot.GroupNumber && s.SlotNumber == slot.SlotNumber)
            .ExecuteDeleteAsync(cancellationToken);

        // Clear tracker to avoid relationship-fixup conflicts from accumulated state.
        context.ChangeTracker.Clear();

        context.RaidCompositionPreviewSlots.Add(slot);
        await TouchUpdatedAtAsync(slot.RaidCompositionPreviewId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ClearSlotAsync(int previewId, int groupNumber, int slotNumber, CancellationToken cancellationToken = default)
    {
        var count = await context.RaidCompositionPreviewSlots
            .Where(s => s.RaidCompositionPreviewId == previewId && s.GroupNumber == groupNumber && s.SlotNumber == slotNumber)
            .ExecuteDeleteAsync(cancellationToken);

        if (count > 0)
            await TouchUpdatedAtAsync(previewId, cancellationToken);

        return count > 0;
    }

    private async Task TouchUpdatedAtAsync(int previewId, CancellationToken cancellationToken)
    {
        await context.RaidCompositionPreviews
            .Where(p => p.Id == previewId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }
}
