using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IGuildBranchesRepository"/>.
/// </summary>
public class GuildBranchesRepository(RaidOpsDbContext context) : IGuildBranchesRepository
{
    /// <inheritdoc/>
    public async Task<GuildBranch?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.GuildBranches.FindAsync([id], cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildBranch?> GetByGuildAndBranchAsync(string guildId, int branchId, CancellationToken cancellationToken = default)
        => await context.GuildBranches
            .FirstOrDefaultAsync(gb => gb.GuildId == guildId && gb.BranchId == branchId, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<GuildBranch>> GetAllForGuildAsync(string guildId, CancellationToken cancellationToken = default)
        => await context.GuildBranches
            .Where(gb => gb.GuildId == guildId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<List<GuildBranch>> GetActiveForGuildAsync(string guildId, CancellationToken cancellationToken = default)
        => await context.GuildBranches
            .Where(gb => gb.GuildId == guildId && gb.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildBranch> ActivateAsync(string guildId, int branchId, CancellationToken cancellationToken = default)
    {
        var existing = await context.GuildBranches
            .FirstOrDefaultAsync(gb => gb.GuildId == guildId && gb.BranchId == branchId, cancellationToken);

        if (existing != null)
        {
            existing.IsActive = true;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var branch = new GuildBranch
        {
            GuildId = guildId,
            BranchId = branchId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        context.GuildBranches.Add(branch);
        await context.SaveChangesAsync(cancellationToken);
        return branch;
    }

    /// <inheritdoc/>
    public async Task<bool> DeactivateAsync(int guildBranchId, CancellationToken cancellationToken = default)
    {
        var branch = await context.GuildBranches.FindAsync([guildBranchId], cancellationToken);
        if (branch == null) return false;

        branch.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateRosterSettingsAsync(
        int guildBranchId,
        RosterMode rosterMode,
        List<string> rosterRoleIds,
        List<string> officerRoleIds,
        CancellationToken cancellationToken = default)
    {
        var branch = await context.GuildBranches.FindAsync([guildBranchId], cancellationToken);
        if (branch == null) return false;

        branch.RosterMode = rosterMode;
        branch.RosterRoleIds = rosterMode == RosterMode.DiscordRoleOnly ? rosterRoleIds : [];
        branch.OfficerRoleIds = officerRoleIds;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
