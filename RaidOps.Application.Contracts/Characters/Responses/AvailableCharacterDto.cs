namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Represents a WoW character returned by the BNet API and available for import.
/// Includes a flag indicating whether the character has already been imported into RaidOps.
/// </summary>
public class AvailableCharacterDto
{
    /// <summary>Blizzard's internal character ID (unique within a realm).</summary>
    public long BnetCharacterId { get; set; }

    /// <summary>Character name as returned by the BNet API.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Realm slug (e.g. "kazzak").</summary>
    public string RealmSlug { get; set; } = string.Empty;

    /// <summary>Localised realm name (e.g. "Kazzak").</summary>
    public string RealmName { get; set; } = string.Empty;

    /// <summary>Blizzard class ID (matches the seeded <c>WowClasses</c> table PKs).</summary>
    public int ClassId { get; set; }

    /// <summary>Class display name as returned by the BNet API.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Blizzard race ID (matches the seeded <c>Races</c> table PKs).</summary>
    public int RaceId { get; set; }

    /// <summary>Race display name as returned by the BNet API.</summary>
    public string RaceName { get; set; } = string.Empty;

    /// <summary>Faction type string as returned by the BNet API (e.g. "ALLIANCE", "HORDE").</summary>
    public string Faction { get; set; } = string.Empty;

    /// <summary>Character level as returned by the BNet API.</summary>
    public int Level { get; set; }

    /// <summary>
    /// <c>true</c> if this character has already been imported into RaidOps by the current user.
    /// Used in the UI to pre-check or grey out the row.
    /// </summary>
    public bool AlreadyImported { get; set; }
}
