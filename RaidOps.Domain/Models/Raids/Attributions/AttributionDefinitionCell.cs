using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Raids.Attributions;

/// <summary>
/// A single ordered cell within a <see cref="GuildAttributionDefinition"/> row — either a
/// display-only <see cref="AttributionCellKind.Icon"/> or a fillable
/// <see cref="AttributionCellKind.NameSlot"/>. A row composes as many cells as it needs (e.g. a
/// simple curse is one icon + one name slot; a personal cooldown like Innervate is one icon + a
/// "Source" slot restricted to Druid + a "Target" slot; a tank/heal-assign row chains two
/// icon+name-slot pairs). Each cell is individually addressable via its surrogate <see cref="Id"/>,
/// which a <see cref="RaidEventAttribution"/> fill targets directly instead of a bare slot index.
/// </summary>
[Table("AttributionDefinitionCells")]
public class AttributionDefinitionCell
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the row this cell belongs to.</summary>
    public int GuildAttributionDefinitionId { get; set; }

    /// <summary>Display ordering within the row.</summary>
    public int CellIndex { get; set; }

    /// <summary>Whether this cell is a display-only icon or a fillable name slot.</summary>
    public AttributionCellKind Kind { get; set; }

    // ── Icon cell fields (Kind == Icon) ─────────────────────────────────────

    /// <summary>What icon-related field to render, when <see cref="Kind"/> is <see cref="AttributionCellKind.Icon"/>.</summary>
    public AttributionIconSource IconSource { get; set; } = AttributionIconSource.None;

    /// <summary>FK to the linked spell, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public int? SpellId { get; set; }

    /// <summary>The raid target marker, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.RaidMarker"/>.</summary>
    public RaidMarkerIcon? RaidMarker { get; set; }

    /// <summary>The built-in role icon to show, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.StaticRole"/>.</summary>
    public SpecRole? StaticRole { get; set; }

    // ── Name-slot cell fields (Kind == NameSlot) ────────────────────────────

    /// <summary>Short label shown on/above the slot (e.g. "Source", "Cible", "Tank"), or <c>null</c>.</summary>
    [MaxLength(32)]
    public string? SlotLabel { get; set; }

    /// <summary>FKs to the WoW classes this slot is restricted to (OR'd together), or empty for no class restriction. Mapped to a native Postgres array — no FK constraint enforced.</summary>
    public List<int> RequiredClassIds { get; set; } = [];

    /// <summary>The roles this slot is restricted to (OR'd together), or empty for no role restriction.</summary>
    public List<SpecRole> RequiredRoles { get; set; } = [];

    /// <summary>FKs to the specific specs this slot is restricted to (OR'd together), or empty for no spec restriction. Mapped to a native Postgres array — no FK constraint enforced.</summary>
    public List<int> RequiredSpecIds { get; set; } = [];

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The row this cell belongs to.</summary>
    public virtual GuildAttributionDefinition GuildAttributionDefinition { get; set; } = null!;

    /// <summary>The linked spell, or <c>null</c> unless <see cref="IconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public virtual Spell? Spell { get; set; }
}
