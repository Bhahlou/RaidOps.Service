using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.Services;

/// <summary>
/// Validates a <c>GuildAttributionDefinition</c>'s cells are internally consistent — shared by the
/// create and update command handlers so the rule lives in exactly one place.
/// </summary>
internal static class AttributionDefinitionValidator
{
    /// <summary>Returns a <see cref="ResponseDetail"/> code describing the first validation failure found, or <c>null</c> if every cell is valid.</summary>
    public static async Task<string?> ValidateAsync(
        List<AttributionCellRequest> cells,
        ISpellRepository spellRepository,
        CancellationToken cancellationToken)
    {
        if (cells.Count == 0)
            return ResponseDetail.NoCellsInDefinition;

        foreach (var cell in cells)
        {
            switch (cell.Kind)
            {
                case AttributionCellKind.Icon:
                    var iconValidation = await ValidateIconAsync(cell, spellRepository, cancellationToken);
                    if (iconValidation != null)
                        return iconValidation;
                    break;

                case AttributionCellKind.NameSlot:
                    break;

                default:
                    return ResponseDetail.InvalidRequest;
            }
        }

        return null;
    }

    private static async Task<string?> ValidateIconAsync(AttributionCellRequest cell, ISpellRepository spellRepository, CancellationToken cancellationToken)
    {
        switch (cell.IconSource)
        {
            case AttributionIconSource.Spell:
                if (cell.SpellId == null)
                    return ResponseDetail.InvalidRequest;
                if (await spellRepository.GetByIdAsync(cell.SpellId.Value, cancellationToken) == null)
                    return ResponseDetail.SpellNotFound;
                return null;

            case AttributionIconSource.RaidMarker:
                return cell.RaidMarker == null ? ResponseDetail.InvalidRequest : null;

            case AttributionIconSource.StaticRole:
                return cell.StaticRole == null ? ResponseDetail.InvalidRequest : null;

            case AttributionIconSource.None:
            default:
                return ResponseDetail.InvalidRequest;
        }
    }
}
