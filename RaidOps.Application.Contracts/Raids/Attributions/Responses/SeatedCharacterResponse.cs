namespace RaidOps.Application.Contracts.Raids.Attributions.Responses;

/// <summary>A character currently seated in a raid event's slot grid — the fill picker's candidate pool.</summary>
public class SeatedCharacterResponse
{
    /// <summary>Internal character ID.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Character name.</summary>
    public required string Name { get; set; }

    /// <summary>FK to the character's WoW class, for icon/color rendering.</summary>
    public required int ClassId { get; set; }

    /// <summary>FK to the spec the character is seated as for this event — lets the fill picker filter by role/spec restrictions client-side.</summary>
    public required int SpecId { get; set; }
}
