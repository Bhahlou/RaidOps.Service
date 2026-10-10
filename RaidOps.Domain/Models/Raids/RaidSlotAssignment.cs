using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Reference;
using WowCharacter = RaidOps.Domain.Models.Character.Character;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A single character assigned to a (group, slot) coordinate of a <see cref="RaidEvent"/>'s grid.
/// Storage is sparse — a coordinate with no row is an empty slot — and there is no separate
/// "slot" entity: the grid's shape is implied by <see cref="RaidEvent.GroupCount"/> x
/// <see cref="RaidEvent.SlotsPerGroup"/>. Kept additive-friendly for a future auto-builder.
/// Composite primary key: (<see cref="RaidEventId"/>, <see cref="GroupNumber"/>, <see cref="SlotNumber"/>).
/// </summary>
[Table("RaidSlotAssignments")]
public class RaidSlotAssignment
{
    /// <summary>FK to the event this assignment belongs to.</summary>
    public int RaidEventId { get; set; }

    /// <summary>1-based group number within the event's grid.</summary>
    public int GroupNumber { get; set; }

    /// <summary>1-based slot number within the group.</summary>
    public int SlotNumber { get; set; }

    /// <summary>FK to the assigned character.</summary>
    public int CharacterId { get; set; }

    /// <summary>
    /// FK to the spec this character is playing for this assignment — defaults to the character's
    /// main raid spec when first assigned, but the officer can switch it to any of the character's
    /// other declared raid specs afterwards (e.g. an off-spec is needed for this particular raid).
    /// </summary>
    public int SpecId { get; set; }

    /// <summary>
    /// Discord snowflake ID of the player who owns <see cref="CharacterId"/>, denormalized from
    /// <see cref="Character.UserDiscordId"/> at assignment time so a DB-level unique index on
    /// (<see cref="RaidEventId"/>, <see cref="AssignedPlayerDiscordId"/>) can enforce "one character
    /// per player per event" in depth, on top of the application-level check.
    /// </summary>
    [Required]
    public string AssignedPlayerDiscordId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when this assignment was made.</summary>
    public DateTime AssignedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who made this assignment.</summary>
    [Required]
    public string AssignedByDiscordId { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The event this assignment belongs to.</summary>
    public virtual RaidEvent RaidEvent { get; set; } = null!;

    /// <summary>The assigned character.</summary>
    public virtual WowCharacter Character { get; set; } = null!;

    /// <summary>The spec this character is playing for this assignment.</summary>
    public virtual Spec Spec { get; set; } = null!;
}
