using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Zones.Responses;

namespace RaidOps.Application.Contracts.Raids.Zones.Queries;

/// <summary>
/// Returns every raid zone available on the expansion currently active for a given guild branch
/// (e.g. SSC/TK only for a TBC branch). The requesting user must hold at least
/// <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>.
/// </summary>
public class GetRaidZonesForBranchQuery : IQueryRequest<List<RaidZoneResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>Surrogate ID of the guild branch whose currently active expansion's raid zones to return.</summary>
    public required int GuildBranchId { get; set; }
}
