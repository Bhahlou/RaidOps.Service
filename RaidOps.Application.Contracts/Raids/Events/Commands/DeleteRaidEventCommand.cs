using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Events.Commands;

/// <summary>
/// Permanently deletes a raid event that has no slot assignments (use
/// <c>CancelRaidEventCommand</c> instead once assignments exist, to preserve history). The
/// requesting user must hold <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on
/// <see cref="GuildId"/>.
/// </summary>
public class DeleteRaidEventCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer deleting this event. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>ID of the event to delete. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }
}
