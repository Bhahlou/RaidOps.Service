namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>A specialisation linked to a character for a given expansion, as reported by Battle.net.</summary>
public class BnetCharacterSpecDto
{
    /// <summary>Blizzard spec ID.</summary>
    public int SpecId { get; set; }

    /// <summary>Spec display name (e.g. "Arms", "Fury").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Icon URL from the Blizzard CDN. <c>null</c> if the sync has not run yet.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Whether this is the character's primary spec. The secondary spec is the offspec.</summary>
    public bool IsMain { get; set; }
}
