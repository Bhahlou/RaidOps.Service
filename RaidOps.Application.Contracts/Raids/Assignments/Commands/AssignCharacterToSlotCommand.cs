using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Assignments.Commands;

/// <summary>
/// Assigns a character to a (group, slot) coordinate of a raid event's grid — rejected if the
/// slot is already occupied (no automatic swap), if the character isn't eligible, or if it
/// conflicts with a declared absence or another event's lockout on a shared zone. The requesting
/// user must hold <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class AssignCharacterToSlotCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild the target event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer making the assignment. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>ID of the target event. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }

    /// <summary>1-based group number within the event's grid.</summary>
    public required int GroupNumber { get; set; }

    /// <summary>1-based slot number within the group.</summary>
    public required int SlotNumber { get; set; }

    /// <summary>ID of the character to assign.</summary>
    public required int CharacterId { get; set; }
}
