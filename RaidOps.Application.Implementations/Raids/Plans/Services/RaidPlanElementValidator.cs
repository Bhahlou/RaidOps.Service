using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.Services;

/// <summary>
/// Validates a <see cref="SaveRaidPlanPageElementsCommand"/>'s elements are internally consistent —
/// each <see cref="RaidPlanElementKind"/> requires its own field group to be populated.
/// </summary>
internal static class RaidPlanElementValidator
{
    /// <summary>Returns a <see cref="ResponseDetail"/> code describing the first validation failure found, or <c>null</c> if every element is valid.</summary>
    public static async Task<string?> ValidateAsync(
        List<RaidPlanElementRequest> elements,
        ISpellRepository spellRepository,
        CancellationToken cancellationToken)
    {
        foreach (var element in elements)
        {
            switch (element.Kind)
            {
                case RaidPlanElementKind.Icon:
                    var iconValidation = await AttributionDefinitionValidator.ValidateIconAsync(
                        element.IconSource, element.SpellId, element.RaidMarker, element.StaticRole, spellRepository, cancellationToken);
                    if (iconValidation != null)
                        return iconValidation;
                    break;

                case RaidPlanElementKind.Text:
                    if (string.IsNullOrWhiteSpace(element.Text))
                        return ResponseDetail.InvalidRequest;
                    break;

                case RaidPlanElementKind.Rectangle:
                case RaidPlanElementKind.Circle:
                case RaidPlanElementKind.Triangle:
                case RaidPlanElementKind.Line:
                case RaidPlanElementKind.Arrow:
                    if (string.IsNullOrWhiteSpace(element.StrokeColor))
                        return ResponseDetail.InvalidRequest;
                    break;

                default:
                    return ResponseDetail.InvalidRequest;
            }

            if ((element.Kind == RaidPlanElementKind.Line || element.Kind == RaidPlanElementKind.Arrow)
                && (element.X2 == null || element.Y2 == null))
                return ResponseDetail.InvalidRequest;
        }

        return null;
    }
}
