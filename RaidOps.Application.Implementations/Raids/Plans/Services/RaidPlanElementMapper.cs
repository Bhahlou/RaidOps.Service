using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Raids.Plans.Responses;
using RaidOps.Domain.Models.Raids.Plans;

namespace RaidOps.Application.Implementations.Raids.Plans.Services;

/// <summary>Maps <see cref="RaidPlanElement"/> to/from its request/response DTOs — shared across the element save handler and the page-detail query handler.</summary>
internal static class RaidPlanElementMapper
{
    /// <summary>Maps a request DTO to an entity. <see cref="RaidPlanElement.Id"/> is 0 for a new element, matching the repository's insert-vs-update check.</summary>
    public static RaidPlanElement ToEntity(RaidPlanElementRequest request) => new()
    {
        Id = request.Id ?? 0,
        Kind = request.Kind,
        ZIndex = request.ZIndex,
        X = request.X,
        Y = request.Y,
        Width = request.Width,
        Height = request.Height,
        RotationDegrees = request.RotationDegrees,
        IconSource = request.IconSource,
        SpellId = request.IconSource == Domain.Enums.AttributionIconSource.Spell ? request.SpellId : null,
        RaidMarker = request.IconSource == Domain.Enums.AttributionIconSource.RaidMarker ? request.RaidMarker : null,
        StaticRole = request.IconSource == Domain.Enums.AttributionIconSource.StaticRole ? request.StaticRole : null,
        Text = request.Kind == Domain.Enums.RaidPlanElementKind.Text ? request.Text : null,
        FontSizeRatio = request.Kind == Domain.Enums.RaidPlanElementKind.Text ? request.FontSizeRatio : null,
        StrokeColor = request.StrokeColor,
        FillColor = request.FillColor,
        StrokeWidthRatio = request.StrokeWidthRatio,
        X2 = request.X2,
        Y2 = request.Y2,
    };

    /// <summary>Maps a list of request DTOs to entities.</summary>
    public static List<RaidPlanElement> ToEntities(IEnumerable<RaidPlanElementRequest> requests) =>
        requests.Select(ToEntity).ToList();

    /// <summary>Maps a persisted entity to its response DTO.</summary>
    public static RaidPlanElementResponse ToResponse(RaidPlanElement element) => new()
    {
        Id = element.Id,
        Kind = element.Kind,
        ZIndex = element.ZIndex,
        X = element.X,
        Y = element.Y,
        Width = element.Width,
        Height = element.Height,
        RotationDegrees = element.RotationDegrees,
        IconSource = element.IconSource,
        SpellId = element.SpellId,
        SpellIconUrl = element.Spell?.IconUrl,
        RaidMarker = element.RaidMarker,
        StaticRole = element.StaticRole,
        Text = element.Text,
        FontSizeRatio = element.FontSizeRatio,
        StrokeColor = element.StrokeColor,
        FillColor = element.FillColor,
        StrokeWidthRatio = element.StrokeWidthRatio,
        X2 = element.X2,
        Y2 = element.Y2,
    };
}
