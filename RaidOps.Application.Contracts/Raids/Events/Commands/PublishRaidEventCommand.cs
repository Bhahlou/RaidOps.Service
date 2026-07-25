using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Events.Commands;

/// <summary>
/// Publishes a raid event, making it visible to non-officer roster members and counting it toward
/// the "unassigned members" computation. The requesting user must hold
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class PublishRaidEventCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer publishing this event. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>ID of the event to publish. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }
}
