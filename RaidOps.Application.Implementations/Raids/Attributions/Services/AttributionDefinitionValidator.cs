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
                    var iconValidation = await ValidateIconAsync(cell.IconSource, cell.SpellId, cell.RaidMarker, cell.StaticRole, spellRepository, cancellationToken);
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

    /// <summary>
    /// Validates a standalone set of icon fields (not attached to a cell) — shared with cell
    /// validation above and with <c>SetAttributionSectionIconCommandHandler</c>'s section-header icon.
    /// </summary>
    /// <param name="allowNone">
    /// Whether <see cref="AttributionIconSource.None"/> is itself a valid state — <c>true</c> for a
    /// section header icon (no icon is a normal, clearable state); <c>false</c> for an icon cell,
    /// which must always resolve to a real icon.
    /// </param>
    public static async Task<string?> ValidateIconAsync(
        AttributionIconSource iconSource,
        int? spellId,
        RaidMarkerIcon? raidMarker,
        SpecRole? staticRole,
        ISpellRepository spellRepository,
        CancellationToken cancellationToken,
        bool allowNone = false)
    {
        switch (iconSource)
        {
            case AttributionIconSource.Spell:
                if (spellId == null)
                    return ResponseDetail.InvalidRequest;
                if (await spellRepository.GetByIdAsync(spellId.Value, cancellationToken) == null)
                    return ResponseDetail.SpellNotFound;
                return null;

            case AttributionIconSource.RaidMarker:
                return raidMarker == null ? ResponseDetail.InvalidRequest : null;

            case AttributionIconSource.StaticRole:
                return staticRole == null ? ResponseDetail.InvalidRequest : null;

            case AttributionIconSource.None:
                return allowNone ? null : ResponseDetail.InvalidRequest;

            default:
                return ResponseDetail.InvalidRequest;
        }
    }
}
