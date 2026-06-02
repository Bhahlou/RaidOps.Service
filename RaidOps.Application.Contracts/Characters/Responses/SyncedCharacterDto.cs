namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Represents a WoW character synced from BNet, used in the import selection dialog.
/// Includes whether the character is already active in RaidOps.
/// </summary>
public class SyncedCharacterDto
{
    /// <summary>Internal RaidOps character identifier.</summary>
    public int Id { get; set; }

    /// <summary>Character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Blizzard class ID.</summary>
    public int ClassId { get; set; }

    /// <summary>Class display name.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Official class colour as a #RRGGBB hex string.</summary>
    public string ClassColor { get; set; } = string.Empty;

    /// <summary>Blizzard race ID.</summary>
    public int RaceId { get; set; }

    /// <summary>Race display name.</summary>
    public string RaceName { get; set; } = string.Empty;

    /// <summary>Faction string in uppercase: "ALLIANCE", "HORDE", or "NEUTRAL".</summary>
    public string Faction { get; set; } = string.Empty;

    /// <summary>Branch display name (e.g. "Classic Anniversary").</summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>Localised realm name.</summary>
    public string RealmName { get; set; } = string.Empty;

    /// <summary>Character level.</summary>
    public int Level { get; set; }

    /// <summary>Whether this character is already active in RaidOps.</summary>
    public bool IsActive { get; set; }
}
