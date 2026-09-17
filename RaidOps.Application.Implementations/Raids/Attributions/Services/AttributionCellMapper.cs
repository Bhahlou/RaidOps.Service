using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Domain.Models.Raids.Attributions;

namespace RaidOps.Application.Implementations.Raids.Attributions.Services;

/// <summary>Maps <see cref="AttributionDefinitionCell"/> to/from its request/response DTOs — shared across the definition command and query handlers.</summary>
internal static class AttributionCellMapper
{
    /// <summary>Maps a request DTO to a new (unsaved) entity. <see cref="AttributionDefinitionCell.CellIndex"/> is set by the caller from the list position.</summary>
    public static AttributionDefinitionCell ToEntity(AttributionCellRequest request, int index) => new()
    {
        CellIndex = index,
        Kind = request.Kind,
        IconSource = request.Kind == Domain.Enums.AttributionCellKind.Icon ? request.IconSource : Domain.Enums.AttributionIconSource.None,
        SpellId = request.IconSource == Domain.Enums.AttributionIconSource.Spell ? request.SpellId : null,
        RaidMarker = request.IconSource == Domain.Enums.AttributionIconSource.RaidMarker ? request.RaidMarker : null,
        StaticRole = request.IconSource == Domain.Enums.AttributionIconSource.StaticRole ? request.StaticRole : null,
        SlotLabel = request.Kind == Domain.Enums.AttributionCellKind.NameSlot ? request.SlotLabel : null,
        RequiredClassIds = request.Kind == Domain.Enums.AttributionCellKind.NameSlot ? request.RequiredClassIds : [],
        RequiredRoles = request.Kind == Domain.Enums.AttributionCellKind.NameSlot ? request.RequiredRoles : [],
        RequiredSpecIds = request.Kind == Domain.Enums.AttributionCellKind.NameSlot ? request.RequiredSpecIds : [],
    };

    /// <summary>Maps an ordered list of request DTOs to new (unsaved) entities, assigning <see cref="AttributionDefinitionCell.CellIndex"/> from position.</summary>
    public static List<AttributionDefinitionCell> ToEntities(IEnumerable<AttributionCellRequest> requests) =>
        requests.Select(ToEntity).ToList();

    /// <summary>Maps a persisted entity to its response DTO.</summary>
    public static AttributionCellResponse ToResponse(AttributionDefinitionCell cell) => new()
    {
        Id = cell.Id,
        Kind = cell.Kind,
        IconSource = cell.IconSource,
        SpellId = cell.SpellId,
        SpellIconUrl = cell.Spell?.IconUrl,
        RaidMarker = cell.RaidMarker,
        StaticRole = cell.StaticRole,
        SlotLabel = cell.SlotLabel,
        RequiredClassIds = cell.RequiredClassIds,
        RequiredRoles = cell.RequiredRoles,
        RequiredSpecIds = cell.RequiredSpecIds,
    };

    /// <summary>Maps a persisted row (with its cells) to its response DTO — shared by every query handler that returns definitions.</summary>
    public static GuildAttributionDefinitionResponse ToDefinitionResponse(GuildAttributionDefinition definition) => new()
    {
        Id = definition.Id,
        Label = definition.Label,
        Section = definition.Section,
        IsRepeatable = definition.IsRepeatable,
        RaidBossId = definition.RaidBossId,
        SectionIconSource = definition.SectionIconSource,
        SectionSpellId = definition.SectionSpellId,
        SectionSpellIconUrl = definition.SectionSpell?.IconUrl,
        SectionRaidMarker = definition.SectionRaidMarker,
        SectionStaticRole = definition.SectionStaticRole,
        Cells = definition.Cells.Select(ToResponse).ToList(),
        SortOrder = definition.SortOrder,
    };
}
