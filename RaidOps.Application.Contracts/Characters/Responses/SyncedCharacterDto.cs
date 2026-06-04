namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Represents a WoW character synced from BNet, used in the import selection dialog.
/// Includes whether the character is already active in RaidOps.
/// </summary>
public class SyncedCharacterDto : CharacterBaseDto
{
    /// <summary>Whether this character is already active in RaidOps.</summary>
    public bool IsActive { get; set; }
}
