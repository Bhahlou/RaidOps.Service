namespace RaidOps.API.Requests;

/// <summary>Request body for <c>POST /api/v1/characters/activate</c>.</summary>
public class ActivateCharactersRequest
{
    /// <summary>RaidOps internal IDs of the characters to activate.</summary>
    public required IEnumerable<int> CharacterIds { get; set; }
}
