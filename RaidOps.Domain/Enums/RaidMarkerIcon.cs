namespace RaidOps.Domain.Enums;

/// <summary>
/// One of WoW's 8 fixed raid target markers, used as an <see cref="AttributionIconSource.RaidMarker"/>
/// icon on a <see cref="Models.Raids.Attributions.GuildAttributionDefinition"/> (e.g. "kill order:
/// Skull" rather than a specific spell). Bundled as static local image assets on the front-end —
/// this fixed set never changes, so there is nothing to seed or fetch.
/// </summary>
public enum RaidMarkerIcon
{
    Skull = 1,
    Cross = 2,
    Square = 3,
    Moon = 4,
    Triangle = 5,
    Diamond = 6,
    Circle = 7,
    Star = 8,
}
