using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IGuildMembershipRepository"/>.
/// </summary>
public class GuildMembershipRepository(RaidOpsDbContext context) : IGuildMembershipRepository
{
    /// <summary>
    /// Returns all roster memberships for the given character, including guild navigation data.
    /// </summary>
    public async Task<List<GuildMembership>> GetByCharacterIdAsync(int characterId, CancellationToken cancellationToken = default)
        => await context.GuildMemberships
            .Include(m => m.Guild)
            .Where(m => m.CharacterId == characterId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns all roster memberships for the given set of characters in a single query.
    /// </summary>
    public async Task<List<GuildMembership>> GetByCharacterIdsAsync(IEnumerable<int> characterIds, CancellationToken cancellationToken = default)
        => await context.GuildMemberships
            .Where(m => characterIds.Contains(m.CharacterId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns all roster memberships for the given guild with character, class, branch, realm,
    /// user-curated raid spec and expansion state (for level) navigation data.
    /// </summary>
    public async Task<List<GuildMembership>> GetByGuildIdAsync(string guildId, CancellationToken cancellationToken = default)
        => await context.GuildMemberships
            .Include(m => m.Character).ThenInclude(c => c.Class)
            .Include(m => m.Character).ThenInclude(c => c.Branch)
            .Include(m => m.Character).ThenInclude(c => c.Realm)
            .Include(m => m.Character).ThenInclude(c => c.RaidSpecs).ThenInclude(rs => rs.Spec)
            .Include(m => m.Character).ThenInclude(c => c.ExpansionStates)
            .Where(m => m.GuildId == guildId && m.Character.IsActiveInRaidOps)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Returns <c>true</c> if the character already has a membership in the given guild.
    /// </summary>
    public async Task<bool> ExistsAsync(int characterId, string guildId, CancellationToken cancellationToken = default)
        => await context.GuildMemberships
            .AnyAsync(m => m.CharacterId == characterId && m.GuildId == guildId, cancellationToken);

    /// <summary>
    /// Persists a new membership record.
    /// </summary>
    public async Task AddAsync(GuildMembership membership, CancellationToken cancellationToken = default)
    {
        context.GuildMemberships.Add(membership);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the membership for the given character/guild pair, or <c>null</c> if not found.
    /// </summary>
    public async Task<GuildMembership?> GetAsync(int characterId, string guildId, CancellationToken cancellationToken = default)
        => await context.GuildMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CharacterId == characterId && m.GuildId == guildId, cancellationToken);

    /// <summary>
    /// Updates the rank of an existing roster membership.
    /// Returns <c>false</c> if the record did not exist.
    /// </summary>
    public async Task<bool> UpdateRankAsync(int characterId, string guildId, CharacterRank rank, CancellationToken cancellationToken = default)
    {
        var count = await context.GuildMemberships
            .Where(m => m.CharacterId == characterId && m.GuildId == guildId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CharacterRank, rank), cancellationToken);
        return count > 0;
    }

    /// <summary>
    /// Removes the membership for the given character/guild pair.
    /// Returns <c>false</c> if the record did not exist.
    /// </summary>
    public async Task<bool> DeleteAsync(int characterId, string guildId, CancellationToken cancellationToken = default)
    {
        var membership = await context.GuildMemberships
            .FirstOrDefaultAsync(m => m.CharacterId == characterId && m.GuildId == guildId, cancellationToken);

        if (membership == null) return false;

        context.GuildMemberships.Remove(membership);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
