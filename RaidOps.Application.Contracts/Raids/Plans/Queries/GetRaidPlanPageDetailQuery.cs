using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Responses;

namespace RaidOps.Application.Contracts.Raids.Plans.Queries;

/// <summary>Returns one page including its elements — the canvas editor's main load query. Viewable at Roster level.</summary>
public class GetRaidPlanPageDetailQuery : IQueryRequest<RaidPlanPageDetailResponse>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>The board the page belongs to. Set by the controller from the route.</summary>
    public int RaidPlanId { get; set; }

    /// <summary>The page to return. Set by the controller from the route.</summary>
    public int RaidPlanPageId { get; set; }
}
