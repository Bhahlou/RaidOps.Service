using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IGuildAttributionDefinitionsRepository"/>.</summary>
public class GuildAttributionDefinitionsRepository(RaidOpsDbContext context) : IGuildAttributionDefinitionsRepository
{
    /// <inheritdoc/>
    public async Task<List<GuildAttributionDefinition>> GetForBranchAsync(string guildId, int guildBranchId, int? raidBossId, CancellationToken cancellationToken = default)
        => await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId && d.GuildBranchId == guildBranchId && d.RaidBossId == raidBossId)
            .Include(d => d.Cells.OrderBy(c => c.CellIndex)).ThenInclude(c => c.Spell).ThenInclude(s => s!.Availabilities) // NOSONAR S9129 — Cells is a collection (filtered include), it cannot be folded into one Include lambda
            .Include(d => d.SectionSpell!.Availabilities)
            .OrderBy(d => d.SortOrder)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildAttributionDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.GuildAttributionDefinitions
            .Include(d => d.Cells.OrderBy(c => c.CellIndex)).ThenInclude(c => c.Spell).ThenInclude(s => s!.Availabilities) // NOSONAR S9129 — Cells is a collection (filtered include), it cannot be folded into one Include lambda
            .Include(d => d.SectionSpell!.Availabilities)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildAttributionDefinition> AddAsync(GuildAttributionDefinition definition, CancellationToken cancellationToken = default)
    {
        // Scoped by (GuildBranchId, RaidBossId) — each branch's "General" rows and each boss's rows
        // are independently numbered, so one list never gets pushed to a huge SortOrder by unrelated rows.
        var siblings = await context.GuildAttributionDefinitions
            .Where(d => d.GuildBranchId == definition.GuildBranchId && d.RaidBossId == definition.RaidBossId)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(cancellationToken);

        // A row joining an *existing* section is inserted right after that section's last row —
        // not always at the very end — so it lands in its own group instead of splitting it into
        // two separate groups further down the display order. A brand-new section (or no section)
        // still just appends at the end, same as before.
        var insertAt = siblings.Count;
        var trimmedSection = definition.Section?.Trim();
        if (!string.IsNullOrEmpty(trimmedSection))
        {
            var lastSectionIndex = siblings.FindLastIndex(d => d.Section != null && d.Section.Trim() == trimmedSection);
            if (lastSectionIndex != -1)
                insertAt = lastSectionIndex + 1;
        }

        siblings.Insert(insertAt, definition);
        for (var i = 0; i < siblings.Count; i++)
            siblings[i].SortOrder = i;

        context.GuildAttributionDefinitions.Add(definition);
        await context.SaveChangesAsync(cancellationToken);
        return definition;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync(GuildAttributionDefinition definition, string guildId, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var existing = await context.GuildAttributionDefinitions
            .Include(d => d.Cells)
            .FirstOrDefaultAsync(d => d.Id == definition.Id && d.GuildId == guildId && d.GuildBranchId == guildBranchId, cancellationToken);
        if (existing == null)
            return false;

        existing.Label = definition.Label;
        existing.Section = definition.Section;
        existing.IsRepeatable = definition.IsRepeatable;

        existing.Cells.Clear();
        foreach (var cell in definition.Cells)
            existing.Cells.Add(cell);

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int id, string guildId, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.GuildAttributionDefinitions
            .Where(d => d.Id == id && d.GuildId == guildId && d.GuildBranchId == guildBranchId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    /// <inheritdoc/>
    public async Task ReorderAsync(string guildId, int guildBranchId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        var definitions = await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId && d.GuildBranchId == guildBranchId && orderedIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (definitions.TryGetValue(orderedIds[i], out var definition))
                definition.SortOrder = i;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SetSectionIconAsync(string guildId, int guildBranchId, int? raidBossId, string section, SectionIconFields icon, CancellationToken cancellationToken = default)
    {
        var trimmedSection = section.Trim();

        return await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId && d.GuildBranchId == guildBranchId && d.RaidBossId == raidBossId && d.Section != null && d.Section.Trim() == trimmedSection)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(d => d.SectionIconSource, icon.IconSource)
                    .SetProperty(d => d.SectionSpellId, icon.SpellId)
                    .SetProperty(d => d.SectionRaidMarker, icon.RaidMarker)
                    .SetProperty(d => d.SectionStaticRole, icon.StaticRole),
                cancellationToken);
    }
}
