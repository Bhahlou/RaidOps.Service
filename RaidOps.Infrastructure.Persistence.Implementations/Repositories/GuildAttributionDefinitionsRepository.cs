using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IGuildAttributionDefinitionsRepository"/>.</summary>
public class GuildAttributionDefinitionsRepository(RaidOpsDbContext context) : IGuildAttributionDefinitionsRepository
{
    /// <inheritdoc/>
    public async Task<List<GuildAttributionDefinition>> GetForGuildAsync(string guildId, int? raidBossId, CancellationToken cancellationToken = default)
        => await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId && d.RaidBossId == raidBossId)
            .Include(d => d.Cells.OrderBy(c => c.CellIndex)).ThenInclude(c => c.Spell)
            .Include(d => d.SectionSpell)
            .OrderBy(d => d.SortOrder)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildAttributionDefinition?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.GuildAttributionDefinitions
            .Include(d => d.Cells.OrderBy(c => c.CellIndex)).ThenInclude(c => c.Spell)
            .Include(d => d.SectionSpell)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<GuildAttributionDefinition> AddAsync(GuildAttributionDefinition definition, CancellationToken cancellationToken = default)
    {
        // Scoped by (GuildId, RaidBossId) — "General" rows and each boss's rows are independently
        // numbered, so a boss's own list never gets pushed to a huge SortOrder by unrelated rows.
        var siblings = await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == definition.GuildId && d.RaidBossId == definition.RaidBossId)
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
    public async Task<bool> UpdateAsync(GuildAttributionDefinition definition, string guildId, CancellationToken cancellationToken = default)
    {
        var existing = await context.GuildAttributionDefinitions
            .Include(d => d.Cells)
            .FirstOrDefaultAsync(d => d.Id == definition.Id && d.GuildId == guildId, cancellationToken);
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
    public async Task<bool> DeleteAsync(int id, string guildId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.GuildAttributionDefinitions
            .Where(d => d.Id == id && d.GuildId == guildId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    /// <inheritdoc/>
    public async Task ReorderAsync(string guildId, IReadOnlyList<int> orderedIds, CancellationToken cancellationToken = default)
    {
        var definitions = await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId && orderedIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (definitions.TryGetValue(orderedIds[i], out var definition))
                definition.SortOrder = i;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> SetSectionIconAsync(
        string guildId,
        int? raidBossId,
        string section,
        AttributionIconSource iconSource,
        int? spellId,
        RaidMarkerIcon? raidMarker,
        SpecRole? staticRole,
        CancellationToken cancellationToken = default)
    {
        var trimmedSection = section.Trim();

        return await context.GuildAttributionDefinitions
            .Where(d => d.GuildId == guildId && d.RaidBossId == raidBossId && d.Section != null && d.Section.Trim() == trimmedSection)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(d => d.SectionIconSource, iconSource)
                    .SetProperty(d => d.SectionSpellId, spellId)
                    .SetProperty(d => d.SectionRaidMarker, raidMarker)
                    .SetProperty(d => d.SectionStaticRole, staticRole),
                cancellationToken);
    }
}
