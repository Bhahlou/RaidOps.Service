using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IRaidBuffDefinitionsRepository"/>.</summary>
public class RaidBuffDefinitionsRepository(RaidOpsDbContext context) : IRaidBuffDefinitionsRepository
{
    /// <inheritdoc/>
    public async Task<List<RaidBuffDefinition>> GetForExpansionAsync(int expansionId, CancellationToken cancellationToken = default)
        => await context.RaidBuffDefinitions
            .Where(d => d.ExpansionId == expansionId)
            .Include(d => d.Sources)
            .Include(d => d.Spell.Availabilities.Where(a => a.ExpansionId == expansionId))
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Id)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<(int Created, int Updated, int Deleted)> UpsertAsync(
        int expansionId,
        IReadOnlyList<RaidBuffDefinition> incoming,
        bool pruneMissing,
        CancellationToken cancellationToken = default)
    {
        var existingBySpell = (await context.RaidBuffDefinitions
            .Where(d => d.ExpansionId == expansionId)
            .Include(d => d.Sources)
            .ToListAsync(cancellationToken))
            .ToDictionary(d => d.SpellId);

        var created = 0;
        var updated = 0;

        foreach (var definition in incoming)
        {
            if (existingBySpell.Remove(definition.SpellId, out var current))
            {
                Overwrite(current, definition);
                updated++;
            }
            else
            {
                definition.ExpansionId = expansionId;
                context.RaidBuffDefinitions.Add(definition);
                created++;
            }
        }

        // Whatever is still in the dictionary was neither matched nor inserted, i.e. absent from the incoming list.
        var deleted = 0;
        if (pruneMissing)
        {
            context.RaidBuffDefinitions.RemoveRange(existingBySpell.Values);
            deleted = existingBySpell.Count;
        }

        await context.SaveChangesAsync(cancellationToken);
        return (created, updated, deleted);
    }

    /// <inheritdoc/>
    public async Task<RaidBuffDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.RaidBuffDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<RaidBuffDefinition?> GetBySpellAsync(int expansionId, int spellId, CancellationToken cancellationToken = default)
        => await context.RaidBuffDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ExpansionId == expansionId && d.SpellId == spellId, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(int id, RaidBuffDefinition incoming, CancellationToken cancellationToken = default)
    {
        var current = await context.RaidBuffDefinitions
            .Include(d => d.Sources)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (current is null)
            return false;

        Overwrite(current, incoming);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await context.RaidBuffDefinitions
            .Where(d => d.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    private void Overwrite(RaidBuffDefinition current, RaidBuffDefinition incoming)
    {
        current.SpellId = incoming.SpellId;
        current.Scope = incoming.Scope;
        current.Kind = incoming.Kind;
        current.LabelEn = incoming.LabelEn;
        current.LabelFr = incoming.LabelFr;
        current.LabelDe = incoming.LabelDe;
        current.ExclusiveGroupKey = incoming.ExclusiveGroupKey;
        current.CapacityPoolKey = incoming.CapacityPoolKey;
        current.SortOrder = incoming.SortOrder;

        context.RaidBuffSources.RemoveRange(current.Sources);
        current.Sources = incoming.Sources;
    }
}
