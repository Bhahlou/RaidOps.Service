using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Responses;

namespace RaidOps.Application.Contracts.Raids.Events.Queries;

/// <summary>
/// Returns a single raid event with its target zones and slot assignments — backs the raid detail
/// page (composition grid, header, self-serve signup). Same shape and visibility rules as
/// <see cref="GetRaidBoardQuery"/>'s per-event mapping, just scoped to one event instead of a date
/// range. The requesting user must hold at least <see cref="Domain.Enums.GuildAccessLevel.Roster"/>
/// access on <see cref="GuildId"/>, and a Draft event stays hidden from non-officers unless it's in
/// Signup mode.
/// </summary>
public class GetRaidEventQuery : IQueryRequest<RaidEventResponse>
{
    /// <summary>Discord snowflake ID of the guild this event belongs to.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch this event belongs to.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>ID of the raid event to fetch.</summary>
    public required int EventId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
