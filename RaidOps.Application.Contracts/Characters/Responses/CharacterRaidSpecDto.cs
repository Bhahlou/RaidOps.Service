namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>A user-curated raid-viable spec linked to a character.</summary>
public class CharacterRaidSpecDto
{
    /// <summary>Blizzard spec ID.</summary>
    public int SpecId { get; set; }

    /// <summary>Spec display name (e.g. "Arms", "Fury").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Icon URL from the Blizzard CDN. <c>null</c> if the icon sync has not run yet.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Whether this is the character's main raid spec.</summary>
    public bool IsMain { get; set; }
}
