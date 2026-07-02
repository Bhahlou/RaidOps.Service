namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// A character's full detail, enriched with the requester's permissions on it —
/// returned by <c>GetCharacterQuery</c> for viewing any character, not just one's own.
/// </summary>
public class CharacterDetailResponse
{
    /// <summary>The character's data.</summary>
    public required CharacterDto Character { get; set; }

    /// <summary>Whether the requester owns this character (grants resync/deactivate/edit raid specs).</summary>
    public required bool IsOwner { get; set; }

    /// <summary>
    /// Whether the requester may edit this character's raid-viable specs — true for the owner,
    /// or an officer of a guild the character is a roster member of.
    /// </summary>
    public required bool CanEditRaidSpecs { get; set; }
}
