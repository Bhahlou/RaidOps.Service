using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Raids.Plans;

/// <summary>
/// One canvas object on a <see cref="RaidPlanPage"/> — polymorphic via <see cref="Kind"/>, same
/// "one table, only-the-matching-fields-are-populated" convention as
/// <see cref="Attributions.AttributionDefinitionCell"/>. Positions/sizes are fractions of the
/// page's background image (<c>[0,1]</c>, center-based), not pixels, so they stay meaningful
/// regardless of the viewport the editor/viewer renders at — as long as the background is always
/// displayed at the aspect ratio declared for it in the front-end's background manifest.
/// </summary>
[Table("RaidPlanElements")]
public class RaidPlanElement
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>FK to the page this element belongs to.</summary>
    public int RaidPlanPageId { get; set; }

    /// <summary>What this element renders as.</summary>
    public RaidPlanElementKind Kind { get; set; }

    /// <summary>
    /// Raw stacking value — higher draws on top. Not densely renumbered like <see cref="RaidPlanPage.SortOrder"/>;
    /// only relative order matters, so bring-to-front/send-to-back just writes a new value.
    /// </summary>
    public int ZIndex { get; set; }

    // ── Position/size — fraction of the page's background image, center-based ──────────────────

    /// <summary>Center X, as a fraction of the background image's width, in <c>[0,1]</c>. For Line/Arrow, the first endpoint.</summary>
    public double X { get; set; }

    /// <summary>Center Y, as a fraction of the background image's height, in <c>[0,1]</c>. For Line/Arrow, the first endpoint.</summary>
    public double Y { get; set; }

    /// <summary>Width as a fraction of the background image's width. Unused for Line/Arrow.</summary>
    public double Width { get; set; }

    /// <summary>Height as a fraction of the background image's height. Unused for Line/Arrow.</summary>
    public double Height { get; set; }

    /// <summary>Rotation in degrees, clockwise. Always 0 for Line/Arrow, which are oriented by their two endpoints instead.</summary>
    public double RotationDegrees { get; set; }

    // ── Icon fields (Kind == Icon) ───────────────────────────────────────────────────────────────

    /// <summary>What icon-related field to render, when <see cref="Kind"/> is <see cref="RaidPlanElementKind.Icon"/>.</summary>
    public AttributionIconSource IconSource { get; set; } = AttributionIconSource.None;

    /// <summary>FK to the linked spell, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public int? SpellId { get; set; }

    /// <summary>The raid target marker, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.RaidMarker"/>.</summary>
    public RaidMarkerIcon? RaidMarker { get; set; }

    /// <summary>The built-in role icon, set only when <see cref="IconSource"/> is <see cref="AttributionIconSource.StaticRole"/>.</summary>
    public SpecRole? StaticRole { get; set; }

    // ── Text fields (Kind == Text) ───────────────────────────────────────────────────────────────

    /// <summary>The label text, set only when <see cref="Kind"/> is <see cref="RaidPlanElementKind.Text"/>.</summary>
    [MaxLength(256)]
    public string? Text { get; set; }

    /// <summary>Font size as a fraction of the page's background image height, set only when <see cref="Kind"/> is <see cref="RaidPlanElementKind.Text"/>.</summary>
    public double? FontSizeRatio { get; set; }

    // ── Shape/line/arrow styling (Kind is a shape, Line, or Arrow) ───────────────────────────────

    /// <summary>Hex stroke color (e.g. "#ffb74d"), set for shape/Line/Arrow kinds.</summary>
    [MaxLength(9)]
    public string? StrokeColor { get; set; }

    /// <summary>Hex fill color, or <c>null</c> for a transparent/outline-only shape. Unused for Line/Arrow/Text.</summary>
    [MaxLength(9)]
    public string? FillColor { get; set; }

    /// <summary>Stroke width as a fraction of the page's background image height, set for shape/Line/Arrow kinds.</summary>
    public double? StrokeWidthRatio { get; set; }

    // ── Line/Arrow fields (Kind == Line or Arrow) ────────────────────────────────────────────────

    /// <summary>Second endpoint's X, same fraction space as <see cref="X"/>, set only for Line/Arrow.</summary>
    public double? X2 { get; set; }

    /// <summary>Second endpoint's Y, same fraction space as <see cref="Y"/>, set only for Line/Arrow.</summary>
    public double? Y2 { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The page this element belongs to.</summary>
    public virtual RaidPlanPage RaidPlanPage { get; set; } = null!;

    /// <summary>The linked spell, or <c>null</c> unless <see cref="IconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public virtual Spell? Spell { get; set; }
}
