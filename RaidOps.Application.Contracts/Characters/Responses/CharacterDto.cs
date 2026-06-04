namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Represents a WoW character imported into RaidOps, used for the character list view.
/// </summary>
public class CharacterDto : CharacterBaseDto
{
    /// <summary>Realm slug used by the BNet API (e.g. "kazzak").</summary>
    public string RealmSlug { get; set; } = string.Empty;

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
