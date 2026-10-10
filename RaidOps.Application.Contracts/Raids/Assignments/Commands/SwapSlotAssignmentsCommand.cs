using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Assignments.Commands;

/// <summary>
/// Swaps the characters occupying two (group, slot) coordinates of the same raid event's grid —
/// the counterpart to <see cref="AssignCharacterToSlotCommand"/>'s "drop onto an occupied slot is
/// rejected" rule: that rule still holds for <see cref="AssignCharacterToSlotCommand"/> itself, but
/// the client now routes an occupied-onto-occupied drag/drop through this command instead of
/// rejecting it outright. Both coordinates must already be occupied; use
/// <see cref="AssignCharacterToSlotCommand"/> for dropping onto an empty slot. The requesting user
/// must hold <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class SwapSlotAssignmentsCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild the target event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer making the swap. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch the target event belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>ID of the target event. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }

    /// <summary>1-based group number of the first coordinate — the slot the drag started from.</summary>
    public required int GroupNumberA { get; set; }

    /// <summary>1-based slot number of the first coordinate — the slot the drag started from.</summary>
    public required int SlotNumberA { get; set; }

    /// <summary>1-based group number of the second coordinate — the slot the drag was dropped onto.</summary>
    public required int GroupNumberB { get; set; }

    /// <summary>1-based slot number of the second coordinate — the slot the drag was dropped onto.</summary>
    public required int SlotNumberB { get; set; }
}
