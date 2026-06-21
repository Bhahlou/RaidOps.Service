namespace RaidOps.API.Requests;

/// <summary>Request body for <c>POST /api/v1/characters/{id}/raid-specs</c>.</summary>
public class SetCharacterRaidSpecsRequest
{
    /// <summary>Blizzard spec ID of the character's main raid spec.</summary>
    public required int MainSpecId { get; set; }

    /// <summary>Blizzard spec IDs the character is viable to raid with.</summary>
    public required IEnumerable<int> ViableSpecIds { get; set; }
}
