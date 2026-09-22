namespace RaidOps.Domain.Enums;

/// <summary>What an <see cref="Models.Raids.Attributions.AttributionDefinitionCell"/> renders as.</summary>
public enum AttributionCellKind
{
    /// <summary>A display-only icon (spell, raid marker, or static role icon).</summary>
    Icon = 1,

    /// <summary>A character assignment slot, optionally restricted by class/role/spec.</summary>
    NameSlot = 2,
}
