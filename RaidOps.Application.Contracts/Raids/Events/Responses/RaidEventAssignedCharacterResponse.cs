namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>One character currently assigned to a raid event — see <see cref="Queries.GetRaidEventAssignedCharactersQuery"/>.</summary>
public class RaidEventAssignedCharacterResponse
{
    /// <summary>Internal character ID.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Character name.</summary>
    public required string Name { get; set; }
}
