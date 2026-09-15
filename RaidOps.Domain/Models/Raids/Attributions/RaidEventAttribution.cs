using System.ComponentModel.DataAnnotations;
using WowCharacter = RaidOps.Domain.Models.Character.Character;

namespace RaidOps.Domain.Models.Raids.Attributions;

/// <summary>
/// A single filled <see cref="AttributionDefinitionCell"/> name slot, for one specific
/// <see cref="RaidEvent"/> and (for a <see cref="GuildAttributionDefinition.IsRepeatable"/> row)
/// one specific instance of it. Storage is sparse — an (event, cell, instance) coordinate with no
/// row is an unfilled slot — directly mirroring <see cref="RaidSlotAssignment"/>'s shape. The
/// assigned character must already have a <see cref="RaidSlotAssignment"/> for the same
/// <see cref="RaidEventId"/> — enforced at the application layer, not by a DB constraint (a
/// composite FK across the two tables doesn't cleanly express "seated in this event") — since
/// attributions fill in what a seated character does, not who's seated. Composite primary key:
/// (<see cref="RaidEventId"/>, <see cref="AttributionDefinitionCellId"/>, <see cref="InstanceIndex"/>).
/// </summary>
public class RaidEventAttribution
{
    /// <summary>FK to the event this fill belongs to.</summary>
    public int RaidEventId { get; set; }

    /// <summary>FK to the name-slot cell this fill instantiates.</summary>
    public int AttributionDefinitionCellId { get; set; }

    /// <summary>
    /// 0-based instance number, for a row whose <see cref="GuildAttributionDefinition.IsRepeatable"/>
    /// is <c>true</c> — how many times its cell pattern has been duplicated for this specific event
    /// (e.g. instance 2 of 3 Innervates). Always 0 for a non-repeatable row.
    /// </summary>
    public int InstanceIndex { get; set; }

    /// <summary>
    /// Denormalized FK to the cell's parent row, redundant with <see cref="AttributionDefinitionCell.GuildAttributionDefinitionId"/>
    /// but kept here (not enforced by its own FK constraint) so callers can filter/group fills by
    /// row without an extra join.
    /// </summary>
    public int GuildAttributionDefinitionId { get; set; }

    /// <summary>FK to the assigned character — must be seated in this event (see class remarks).</summary>
    public int CharacterId { get; set; }

    /// <summary>UTC timestamp of when this fill was made.</summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who made this fill.</summary>
    [Required]
    public string AssignedByDiscordId { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The event this fill belongs to.</summary>
    public virtual RaidEvent RaidEvent { get; set; } = null!;

    /// <summary>The name-slot cell this fill instantiates.</summary>
    public virtual AttributionDefinitionCell AttributionDefinitionCell { get; set; } = null!;

    /// <summary>The assigned character.</summary>
    public virtual WowCharacter Character { get; set; } = null!;
}
