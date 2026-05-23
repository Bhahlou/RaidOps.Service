using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICharacterRepository"/>.
/// </summary>
public class CharacterRepository(RaidOpsDbContext context) : ICharacterRepository
{
    /// <summary>
    /// Returns all characters owned by the given user with their realm, class, race,
    /// and expansion states included. Ordered alphabetically by name.
    /// </summary>
    public async Task<IEnumerable<Character>> GetByUserWithDetailsAsync(string userDiscordId, CancellationToken cancellationToken = default)
    {
        return await context.Characters
            .AsNoTracking()
            .Where(c => c.UserDiscordId == userDiscordId)
            .Include(c => c.Realm)
            .Include(c => c.Class)
            .Include(c => c.Race)
            .Include(c => c.ExpansionStates)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the set of BNet character IDs already imported by the given user.
    /// Used during the import flow to mark characters already present in RaidOps.
    /// </summary>
    public async Task<HashSet<long>> GetBnetIdsByUserAsync(string userDiscordId, CancellationToken cancellationToken = default)
    {
        var ids = await context.Characters
            .AsNoTracking()
            .Where(c => c.UserDiscordId == userDiscordId)
            .Select(c => c.BnetCharacterId)
            .ToListAsync(cancellationToken);

        return [.. ids];
    }

    /// <summary>
    /// Inserts or updates the character identified by its (BnetCharacterId, RealmId) unique key.
    /// Returns the persisted entity with its DB-generated <c>Id</c>.
    /// </summary>
    public async Task<Character> UpsertAsync(Character character, CancellationToken cancellationToken = default)
    {
        var existing = await context.Characters
            .FirstOrDefaultAsync(
                c => c.BnetCharacterId == character.BnetCharacterId && c.RealmId == character.RealmId,
                cancellationToken);

        if (existing is null)
        {
            context.Characters.Add(character);
        }
        else
        {
            existing.Name = character.Name;
            existing.Faction = character.Faction;
            existing.RaceId = character.RaceId;
            existing.ClassId = character.ClassId;
            character = existing;
        }

        await context.SaveChangesAsync(cancellationToken);
        return character;
    }

    /// <summary>
    /// Inserts or updates the expansion state for a (character × expansion) pair.
    /// </summary>
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
}
