using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Attributions.Responses;

/// <summary>A single row of a guild's raid-attribution template.</summary>
public class GuildAttributionDefinitionResponse
{
    /// <summary>Surrogate ID of the definition.</summary>
    public int Id { get; set; }

    /// <summary>Display label.</summary>
    public required string Label { get; set; }

    /// <summary>Free-text grouping label, or <c>null</c> for an ungrouped row.</summary>
    public string? Section { get; set; }

    /// <summary>Whether officers can add a variable number of instances of this row per raid event.</summary>
    public bool IsRepeatable { get; set; }

    /// <summary>FK to the boss this row is scoped to, or <c>null</c> for a "General" row shown on every raid event.</summary>
    public int? RaidBossId { get; set; }

    /// <summary>What icon-related field to render for this row's section header, independent of any cell icon.</summary>
    public AttributionIconSource SectionIconSource { get; set; }

    /// <summary>FK to the section header's linked spell, set only when <see cref="SectionIconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public int? SectionSpellId { get; set; }

    /// <summary>The section header's linked spell's icon URL, denormalized for convenience.</summary>
    public string? SectionSpellIconUrl { get; set; }

    /// <summary>The section header's raid target marker, set only when <see cref="SectionIconSource"/> is <see cref="AttributionIconSource.RaidMarker"/>.</summary>
    public RaidMarkerIcon? SectionRaidMarker { get; set; }

    /// <summary>The section header's built-in role icon, set only when <see cref="SectionIconSource"/> is <see cref="AttributionIconSource.StaticRole"/>.</summary>
    public SpecRole? SectionStaticRole { get; set; }

    /// <summary>The row's ordered cells (icons and name slots).</summary>
    public required List<AttributionCellResponse> Cells { get; set; }

    /// <summary>Display ordering within the guild's template.</summary>
    public int SortOrder { get; set; }
}
