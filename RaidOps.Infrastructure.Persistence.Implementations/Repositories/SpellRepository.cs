using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="ISpellRepository"/>.</summary>
public class SpellRepository(RaidOpsDbContext context) : ISpellRepository
{
    /// <inheritdoc/>
    public async Task<SpellAvailability?> GetAvailabilityAsync(int spellId, int expansionId, CancellationToken cancellationToken = default)
        => await context.SpellAvailabilities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.SpellId == spellId && a.ExpansionId == expansionId, cancellationToken);

    /// <inheritdoc/>
    public async Task<List<SpellAvailability>> SearchAsync(int expansionId, string searchTerm, string locale, int limit, CancellationToken cancellationToken = default)
    {
        var query = context.SpellAvailabilities.Where(a => a.ExpansionId == expansionId);

        query = locale switch
        {
            "fr" => query.Where(a => EF.Functions.ILike(a.NameFr, $"%{searchTerm}%")),
            "de" => query.Where(a => EF.Functions.ILike(a.NameDe, $"%{searchTerm}%")),
            _ => query.Where(a => EF.Functions.ILike(a.NameEn, $"%{searchTerm}%")),
        };

        return await query
            .OrderBy(a => locale == "fr" ? a.NameFr : locale == "de" ? a.NameDe : a.NameEn)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SpellSyncDiff> UpsertAsync(IEnumerable<SpellAvailability> rows, CancellationToken cancellationToken = default)
    {
        var incoming = rows.ToList();
        var incomingIds = incoming.Select(r => r.SpellId).Distinct().ToList();
        var incomingExpansionIds = incoming.Select(r => r.ExpansionId).Distinct().ToList();

        var existingSpellIds = (await context.Spells
            .Where(s => incomingIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        var existingRows = await context.SpellAvailabilities
            .Where(a => incomingIds.Contains(a.SpellId) && incomingExpansionIds.Contains(a.ExpansionId))
            .ToDictionaryAsync(a => (a.SpellId, a.ExpansionId), cancellationToken);

        var diff = new SpellSyncDiff();
        var newSpells = new List<Spell>();
        var newRows = new List<SpellAvailability>();

        foreach (var row in incoming)
        {
            if (existingSpellIds.Add(row.SpellId))
                newSpells.Add(new Spell { Id = row.SpellId });

            if (!existingRows.TryGetValue((row.SpellId, row.ExpansionId), out var existing))
            {
                newRows.Add(row);
                diff.Added.Add(new SpellSyncEntry { SpellId = row.SpellId, NameEn = row.NameEn });
                continue;
            }

            if (existing.NameEn != row.NameEn)
                diff.Renamed.Add(new SpellSyncEntry { SpellId = row.SpellId, NameEn = row.NameEn, PreviousNameEn = existing.NameEn });

            existing.NameEn = row.NameEn;
            existing.NameFr = row.NameFr;
            existing.NameDe = row.NameDe;

            // An empty incoming icon means the icon couldn't be resolved this run (e.g. a transient
            // wago.tools failure) — never let that blank out a previously resolved icon.
            if (row.IconUrl.Length > 0)
                existing.IconUrl = row.IconUrl;
        }

        context.Spells.AddRange(newSpells);
        context.SpellAvailabilities.AddRange(newRows);

        await context.SaveChangesAsync(cancellationToken);
        return diff;
    }
}
