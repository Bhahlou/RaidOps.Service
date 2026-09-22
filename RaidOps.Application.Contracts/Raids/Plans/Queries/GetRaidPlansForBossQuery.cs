using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Responses;

namespace RaidOps.Application.Contracts.Raids.Plans.Queries;

/// <summary>Returns every strategy board the guild has for one boss. Viewable at Roster level.</summary>
public class GetRaidPlansForBossQuery : IQueryRequest<List<RaidPlanResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>The boss to return boards for. Set by the controller from the query string.</summary>
    public int RaidBossId { get; set; }
}
