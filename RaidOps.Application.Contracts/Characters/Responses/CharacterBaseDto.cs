namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Common character fields shared across character DTOs.
/// </summary>
public abstract class CharacterBaseDto
{
    /// <summary>Internal RaidOps character identifier.</summary>
    public int Id { get; set; }

    /// <summary>Character name (e.g. "Arthas").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Blizzard class ID.</summary>
    public int ClassId { get; set; }

    /// <summary>Class display name (e.g. "Death Knight").</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Official class colour as a #RRGGBB hex string (e.g. "#C41E3A").</summary>
    public string ClassColor { get; set; } = string.Empty;

    /// <summary>Blizzard race ID.</summary>
    public int RaceId { get; set; }

    /// <summary>Race display name (e.g. "Blood Elf").</summary>
    public string RaceName { get; set; } = string.Empty;

    /// <summary>Faction string in uppercase: "ALLIANCE", "HORDE", or "NEUTRAL".</summary>
    public string Faction { get; set; } = string.Empty;

    /// <summary>Display name of the game branch (e.g. "Classic Anniversary", "Retail").</summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>Localised realm name (e.g. "Kazzak").</summary>
    public string RealmName { get; set; } = string.Empty;

    /// <summary>Character level.</summary>
    public int Level { get; set; }
}
