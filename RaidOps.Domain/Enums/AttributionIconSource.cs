namespace RaidOps.Domain.Enums;

/// <summary>
/// What a <see cref="Models.Raids.Attributions.GuildAttributionDefinition"/>'s icon is sourced
/// from.
/// </summary>
public enum AttributionIconSource
{
    /// <summary>No icon — the row is displayed with its <c>Label</c> only.</summary>
    None = 1,

    /// <summary>Icon comes from the linked <see cref="Models.Reference.Spell"/>.</summary>
    Spell = 2,

    /// <summary>Icon is one of the 8 fixed <see cref="RaidMarkerIcon"/> raid target markers.</summary>
    RaidMarker = 3,

    /// <summary>Icon is a fixed built-in role icon (Tank/Healer/Dps), not tied to any spell.</summary>
    StaticRole = 4,
}
