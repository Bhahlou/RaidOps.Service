using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Zones.Responses;

namespace RaidOps.Application.Contracts.Raids.Zones.Queries;

/// <summary>
/// Returns the union of raid zones available across every active branch of a guild (deduplicated by
/// zone ID), regardless of branch — backs the guild-wide attribution template editor's "raid" scope
/// picker, which lives outside any single branch's route context (unlike
/// <see cref="GetRaidZonesForBranchQuery"/>). Officer-only, same gate as the template editor itself.
/// </summary>
public class GetRaidZonesForGuildQuery : IQueryRequest<List<RaidZoneResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }
}
