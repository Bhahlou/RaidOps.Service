using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Attributions.Responses;

/// <summary>One cell of a <see cref="GuildAttributionDefinitionResponse"/> row, in display order.</summary>
public class AttributionCellResponse
{
    /// <summary>Surrogate ID of the cell.</summary>
    public int Id { get; set; }

    /// <summary>Whether this cell is a display-only icon or a fillable name slot.</summary>
    public AttributionCellKind Kind { get; set; }

    /// <summary>What icon-related field to render, when <see cref="Kind"/> is <see cref="AttributionCellKind.Icon"/>.</summary>
    public AttributionIconSource IconSource { get; set; }

    /// <summary>FK to the linked spell, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public int? SpellId { get; set; }

    /// <summary>The linked spell's icon URL, denormalized for convenience.</summary>
    public string? SpellIconUrl { get; set; }

    /// <summary>The raid target marker, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.RaidMarker"/>.</summary>
    public RaidMarkerIcon? RaidMarker { get; set; }

    /// <summary>The built-in role icon, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.StaticRole"/>.</summary>
    public SpecRole? StaticRole { get; set; }

    /// <summary>Short label shown on/above the slot, when <see cref="Kind"/> is <see cref="AttributionCellKind.NameSlot"/>.</summary>
    public string? SlotLabel { get; set; }

    /// <summary>FKs to the WoW classes this slot is restricted to (OR'd together), or empty for no class restriction.</summary>
    public List<int> RequiredClassIds { get; set; } = [];

    /// <summary>The roles this slot is restricted to (OR'd together), or empty for no role restriction.</summary>
    public List<SpecRole> RequiredRoles { get; set; } = [];

    /// <summary>FKs to the specific specs this slot is restricted to (OR'd together), or empty for no spec restriction.</summary>
    public List<int> RequiredSpecIds { get; set; } = [];
}
