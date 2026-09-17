using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Attributions.Commands;

/// <summary>
/// Sets the section-header icon shown above every row sharing one (guild, boss scope, section)
/// tuple — independent of any cell icon on the rows themselves.
/// </summary>
public class SetAttributionSectionIconCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Scope of the section being edited — the boss's ID, or <c>null</c> for a "General" section.</summary>
    public int? RaidBossId { get; set; }

    /// <summary>The section (trimmed for matching) to set the icon for.</summary>
    public required string Section { get; set; }

    /// <summary>What icon-related field to render, or <see cref="AttributionIconSource.None"/> to clear the section icon.</summary>
    public AttributionIconSource IconSource { get; set; } = AttributionIconSource.None;

    /// <summary>FK to the linked spell, required when <see cref="IconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public int? SpellId { get; set; }

    /// <summary>The raid target marker, required when <see cref="IconSource"/> is <see cref="AttributionIconSource.RaidMarker"/>.</summary>
    public RaidMarkerIcon? RaidMarker { get; set; }

    /// <summary>The built-in role icon, required when <see cref="IconSource"/> is <see cref="AttributionIconSource.StaticRole"/>.</summary>
    public SpecRole? StaticRole { get; set; }
}
