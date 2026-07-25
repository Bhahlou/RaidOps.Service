using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>A single filled (group, slot) coordinate of a raid event's grid.</summary>
public class RaidSlotAssignmentResponse
{
    /// <summary>1-based group number within the event's grid.</summary>
    public required int GroupNumber { get; set; }

    /// <summary>1-based slot number within the group.</summary>
    public required int SlotNumber { get; set; }

    /// <summary>Internal character ID.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Character name.</summary>
    public required string CharacterName { get; set; }

    /// <summary>FK to the character's class.</summary>
    public required int ClassId { get; set; }

    /// <summary>Hex color of the character's class, prefixed with '#'.</summary>
    public required string ClassColor { get; set; }

    /// <summary>Discord snowflake ID of the player who owns this character.</summary>
    public required string PlayerDiscordId { get; set; }

    /// <summary>Discord display name of the player, or <c>null</c> if it could not be resolved.</summary>
    public string? PlayerName { get; set; }

    /// <summary>
    /// The assigned player's resolved availability on the event's guild-local date, computed at
    /// read time so a declaration made after assignment still surfaces as a conflict in the UI.
    /// </summary>
    public required DayAvailabilityStatus AvailabilityStatus { get; set; }
}
