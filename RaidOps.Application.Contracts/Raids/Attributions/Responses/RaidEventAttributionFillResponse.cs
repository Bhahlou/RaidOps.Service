namespace RaidOps.Application.Contracts.Raids.Attributions.Responses;

/// <summary>One filled slot of a raid event's attributions.</summary>
public class RaidEventAttributionFillResponse
{
    /// <summary>FK to the guild's template row this fill instantiates.</summary>
    public required int DefinitionId { get; set; }

    /// <summary>FK to the name-slot cell this fill instantiates.</summary>
    public required int CellId { get; set; }

    /// <summary>0-based instance number, for a repeatable row. Always 0 for a non-repeatable row.</summary>
    public required int InstanceIndex { get; set; }

    /// <summary>FK to the assigned character.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Assigned character's name, denormalized for display.</summary>
    public required string CharacterName { get; set; }

    /// <summary>Assigned character's WoW class, denormalized for icon/color rendering.</summary>
    public required int ClassId { get; set; }
}
