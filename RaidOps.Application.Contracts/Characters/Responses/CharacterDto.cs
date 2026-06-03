namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Represents a WoW character imported into RaidOps, used for the character list view.
/// </summary>
public class CharacterDto
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

    /// <summary>Realm slug used by the BNet API (e.g. "kazzak").</summary>
    public string RealmSlug { get; set; } = string.Empty;

    /// <summary>
    /// Character level derived from the active expansion state,
    /// or the highest level across all expansion states.
    /// Returns 0 if no expansion state is recorded yet.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Average equipped item level from the active expansion state.
    /// <c>null</c> if not available (e.g. Classic branches).
    /// </summary>
    public int? ItemLevel { get; set; }

    /// <summary>Avatar image URL from the BNet character-media endpoint. <c>null</c> if not yet fetched.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>In-game guild name from the active expansion state. <c>null</c> if unguilded or not yet fetched.</summary>
    public string? GuildName { get; set; }

    /// <summary>Active specialisations for this character (main spec + optional offspec).</summary>
    public List<CharacterSpecDto> Specs { get; set; } = [];
}
