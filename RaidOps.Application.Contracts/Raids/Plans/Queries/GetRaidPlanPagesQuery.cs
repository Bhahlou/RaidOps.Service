using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Responses;

namespace RaidOps.Application.Contracts.Raids.Plans.Queries;

/// <summary>Returns a board's page-tab list (no elements). Viewable at Roster level.</summary>
public class GetRaidPlanPagesQuery : IQueryRequest<List<RaidPlanPageResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>The board to return pages for. Set by the controller from the route.</summary>
    public int RaidPlanId { get; set; }
}
