using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="ISpellRepository"/>.</summary>
public class SpellRepository(RaidOpsDbContext context) : ISpellRepository
{
    /// <inheritdoc/>
    public async Task<Spell?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Spells.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<Spell>> SearchAsync(int expansionId, string searchTerm, string locale, int limit, CancellationToken cancellationToken = default)
    {
        var query = context.Spells.Where(s => s.ExpansionId == expansionId);

        query = locale switch
        {
            "fr" => query.Where(s => EF.Functions.ILike(s.NameFr, $"%{searchTerm}%")),
            "de" => query.Where(s => EF.Functions.ILike(s.NameDe, $"%{searchTerm}%")),
            _ => query.Where(s => EF.Functions.ILike(s.NameEn, $"%{searchTerm}%")),
        };

        return await query
            .OrderBy(s => locale == "fr" ? s.NameFr : locale == "de" ? s.NameDe : s.NameEn)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> InsertMissingAsync(IEnumerable<Spell> spells, CancellationToken cancellationToken = default)
    {
        var incoming = spells.ToList();
        var incomingIds = incoming.Select(s => s.Id).ToList();

        var existingIds = await context.Spells
            .Where(s => incomingIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        var existingIdSet = existingIds.ToHashSet();

        var missing = incoming.Where(s => !existingIdSet.Contains(s.Id)).ToList();
        if (missing.Count == 0)
            return 0;

        context.Spells.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }
}
