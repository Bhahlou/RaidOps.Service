using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;

namespace RaidOps.Application.Contracts.Raids.Attributions.Queries;

/// <summary>
/// Returns the guild's raid-attribution template — every <c>GuildAttributionDefinition</c> row,
/// ordered by <c>SortOrder</c>. Officer-gated: the template editor is officer-only.
/// </summary>
public class GetGuildAttributionDefinitionsQuery : IQueryRequest<List<GuildAttributionDefinitionResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }
}
