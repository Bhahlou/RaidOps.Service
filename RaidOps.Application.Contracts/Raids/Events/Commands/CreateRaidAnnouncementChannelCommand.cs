using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Events.Commands;

/// <summary>
/// Creates a new Discord text channel in the guild, for the officer to immediately pick as a raid's
/// dedicated announcement channel — a convenience over the manual channel picker. The requesting
/// user must hold <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// Returns the created channel (id/name) via <c>CommandResponse.Body</c>, same pattern every other
/// creation command here uses.
/// </summary>
public class CreateRaidAnnouncementChannelCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild to create the channel in. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer requesting the channel. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this channel is being created for. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Requested channel name.</summary>
    public required string Name { get; set; }

    /// <summary>Discord snowflake ID of the category to nest the new channel under, or <c>null</c> to create it at the top level.</summary>
    public string? CategoryId { get; set; }
}
