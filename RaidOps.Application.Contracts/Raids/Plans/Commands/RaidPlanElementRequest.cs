using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>One canvas element of a <see cref="SaveRaidPlanPageElementsCommand"/>. <see cref="Id"/> is <c>null</c> for a newly-added element.</summary>
public class RaidPlanElementRequest
{
    /// <summary>Existing element's ID to update, or <c>null</c> to insert a new element.</summary>
    public int? Id { get; set; }

    /// <summary>What this element renders as.</summary>
    public RaidPlanElementKind Kind { get; set; }

    /// <summary>Stacking order — higher draws on top.</summary>
    public int ZIndex { get; set; }

    /// <summary>Center X (first endpoint for Line/Arrow), as a fraction of the background image's width, in [0,1].</summary>
    public double X { get; set; }

    /// <summary>Center Y (first endpoint for Line/Arrow), as a fraction of the background image's height, in [0,1].</summary>
    public double Y { get; set; }

    /// <summary>Width as a fraction of the background image's width. Unused for Line/Arrow.</summary>
    public double Width { get; set; }

    /// <summary>Height as a fraction of the background image's height. Unused for Line/Arrow.</summary>
    public double Height { get; set; }

    /// <summary>Rotation in degrees, clockwise. Unused for Line/Arrow.</summary>
    public double RotationDegrees { get; set; }

    /// <summary>What icon-related field to render, required when <see cref="Kind"/> is <see cref="RaidPlanElementKind.Icon"/>.</summary>
    public AttributionIconSource IconSource { get; set; } = AttributionIconSource.None;

    /// <summary>FK to the linked spell, required when <see cref="IconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public int? SpellId { get; set; }

    /// <summary>The raid target marker, required when <see cref="IconSource"/> is <see cref="AttributionIconSource.RaidMarker"/>.</summary>
    public RaidMarkerIcon? RaidMarker { get; set; }

    /// <summary>The built-in role icon, required when <see cref="IconSource"/> is <see cref="AttributionIconSource.StaticRole"/>.</summary>
    public SpecRole? StaticRole { get; set; }

    /// <summary>The label text, required when <see cref="Kind"/> is <see cref="RaidPlanElementKind.Text"/>.</summary>
    public string? Text { get; set; }

    /// <summary>Font size as a fraction of the page's background image height, required when <see cref="Kind"/> is <see cref="RaidPlanElementKind.Text"/>.</summary>
    public double? FontSizeRatio { get; set; }

    /// <summary>Hex stroke color, required for shape/Line/Arrow kinds.</summary>
    public string? StrokeColor { get; set; }

    /// <summary>Hex fill color, or <c>null</c> for a transparent/outline-only shape. Unused for Line/Arrow/Text.</summary>
    public string? FillColor { get; set; }

    /// <summary>Stroke width as a fraction of the page's background image height, required for shape/Line/Arrow kinds.</summary>
    public double? StrokeWidthRatio { get; set; }

    /// <summary>Second endpoint's X, same fraction space as <see cref="X"/>, required for Line/Arrow.</summary>
    public double? X2 { get; set; }

    /// <summary>Second endpoint's Y, same fraction space as <see cref="Y"/>, required for Line/Arrow.</summary>
    public double? Y2 { get; set; }
}
