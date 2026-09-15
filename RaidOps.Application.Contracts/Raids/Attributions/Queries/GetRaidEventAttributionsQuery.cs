using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;

namespace RaidOps.Application.Contracts.Raids.Attributions.Queries;

/// <summary>Returns the guild's attribution template merged with a raid event's existing fills and seated characters — backs the raid detail page's Assignments tab.</summary>
public class GetRaidEventAttributionsQuery : IQueryRequest<RaidEventAttributionsResponse>
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>Surrogate ID of the guild branch the event belongs to.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Surrogate ID of the raid event.</summary>
    public required int EventId { get; set; }
}
