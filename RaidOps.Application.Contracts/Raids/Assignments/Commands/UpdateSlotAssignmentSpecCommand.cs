using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Assignments.Commands;

/// <summary>
/// Changes the spec an already-assigned character is playing at a (group, slot) coordinate — e.g.
/// switching to an off-spec this particular raid needs. Rejected if the slot is empty or the spec
/// isn't one of the character's declared raid-viable specs. The requesting user must hold
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class UpdateSlotAssignmentSpecCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild the target event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer making the change. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch the target event belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>ID of the target event. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }

    /// <summary>1-based group number within the event's grid.</summary>
    public required int GroupNumber { get; set; }

    /// <summary>1-based slot number within the group.</summary>
    public required int SlotNumber { get; set; }

    /// <summary>ID of the spec to switch the assignment to — must be one of the character's declared raid specs.</summary>
    public required int SpecId { get; set; }
}
