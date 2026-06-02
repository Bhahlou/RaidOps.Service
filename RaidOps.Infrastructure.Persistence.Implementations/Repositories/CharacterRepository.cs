using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;
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
                .ThenInclude(s => s.Specs)
                    .ThenInclude(cs => cs.Spec)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Character>> GetByIdsWithDetailsAsync(
        IEnumerable<int> ids,
        string userDiscordId,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await context.Characters
            .AsNoTracking()
            .Where(c => idList.Contains(c.Id) && c.UserDiscordId == userDiscordId)
            .Include(c => c.Realm)
            .Include(c => c.Branch)
            .Include(c => c.ExpansionStates)
                .ThenInclude(s => s.Specs)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Spec?> GetSpecByIdAsync(int specId, CancellationToken cancellationToken = default)
    {
        return await context.Specs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == specId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Spec?> GetSpecByNameAndClassAsync(string name, int classId, CancellationToken cancellationToken = default)
    {
        // Classic API returns tree names like "Feral Combat" while the DB stores the canonical retail name "Feral".
        // Match if the API name starts with the stored name (handles the " Combat" suffix and similar variants).
        return await context.Specs
            .AsNoTracking()
            .Where(s => s.ClassId == classId && name.StartsWith(s.Name))
            .OrderByDescending(s => s.Name.Length) // prefer the most specific match
            .FirstOrDefaultAsync(cancellationToken);
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
            existing.AvatarUrl = character.AvatarUrl;
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
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            existing.Level = state.Level;
            existing.ItemLevel = state.ItemLevel;
            existing.IsActive = state.IsActive;
            existing.GuildName = state.GuildName;
            await context.SaveChangesAsync(cancellationToken);

            if (state.Specs.Count > 0)
            {
                var existingSpecs = await context.CharacterSpecs
                    .Where(s => s.CharacterExpansionStateId == existing.Id)
                    .ToListAsync(cancellationToken);
                context.CharacterSpecs.RemoveRange(existingSpecs);

                foreach (var spec in state.Specs)
                    spec.CharacterExpansionStateId = existing.Id;
                context.CharacterSpecs.AddRange(state.Specs);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
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
