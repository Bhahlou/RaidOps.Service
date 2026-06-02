using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICharacterRepository"/>.
/// </summary>
public class CharacterRepository(RaidOpsDbContext context) : ICharacterRepository
{
    /// <inheritdoc/>
    public async Task<IEnumerable<Character>> GetByUserWithDetailsAsync(
        string userDiscordId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = context.Characters
            .AsNoTracking()
            .Where(c => c.UserDiscordId == userDiscordId);

        if (activeOnly)
            query = query.Where(c => c.IsActiveInRaidOps);

        return await query
            .Include(c => c.Branch)
            .Include(c => c.Realm)
            .Include(c => c.Class)
            .Include(c => c.Race)
            .Include(c => c.ExpansionStates)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<HashSet<long>> GetBnetIdsByUserAsync(string userDiscordId, CancellationToken cancellationToken = default)
    {
        var ids = await context.Characters
            .AsNoTracking()
            .Where(c => c.UserDiscordId == userDiscordId)
            .Select(c => c.BnetCharacterId)
            .ToListAsync(cancellationToken);

        return [.. ids];
    }

    /// <inheritdoc/>
    public async Task<Character> UpsertAsync(Character character, CancellationToken cancellationToken = default)
    {
        var existing = await context.Characters
            .FirstOrDefaultAsync(
                c => c.BnetCharacterId == character.BnetCharacterId && c.BranchId == character.BranchId,
                cancellationToken);

        if (existing is null)
        {
            context.Characters.Add(character);
        }
        else
        {
            existing.Name = character.Name;
            existing.Faction = character.Faction;
            existing.Gender = character.Gender;
            existing.RealmId = character.RealmId;
            existing.RaceId = character.RaceId;
            existing.ClassId = character.ClassId;
            // IsActiveInRaidOps is intentionally not updated here — use ActivateAsync.
            character = existing;
        }

        await context.SaveChangesAsync(cancellationToken);
        return character;
    }

    /// <inheritdoc/>
    public async Task UpsertExpansionStateAsync(CharacterExpansionState state, CancellationToken cancellationToken = default)
    {
        var existing = await context.CharacterExpansionStates
            .FirstOrDefaultAsync(
                s => s.CharacterId == state.CharacterId && s.ExpansionId == state.ExpansionId,
                cancellationToken);

        if (existing is null)
        {
            context.CharacterExpansionStates.Add(state);
        }
        else
        {
            existing.Level = state.Level;
            existing.ItemLevel = state.ItemLevel;
            existing.IsActive = state.IsActive;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(IEnumerable<int> characterIds, string userDiscordId, CancellationToken cancellationToken = default)
    {
        var ids = characterIds.ToList();

        var characters = await context.Characters
            .Where(c => ids.Contains(c.Id) && c.UserDiscordId == userDiscordId)
            .ToListAsync(cancellationToken);

        foreach (var character in characters)
            character.IsActiveInRaidOps = true;

        await context.SaveChangesAsync(cancellationToken);
    }
}
