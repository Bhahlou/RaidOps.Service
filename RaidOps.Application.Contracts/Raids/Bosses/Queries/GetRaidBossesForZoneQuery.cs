using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Bosses.Responses;

namespace RaidOps.Application.Contracts.Raids.Bosses.Queries;

/// <summary>
/// Returns every boss encounter of a single raid zone — backs the guild-wide attribution template
/// editor's boss picker (officer-only, guild-wide, no branch context).
/// </summary>
public class GetRaidBossesForZoneQuery : IQueryRequest<List<RaidBossResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>Internal ID of the zone whose bosses to return.</summary>
    public required int RaidZoneId { get; set; }
}
