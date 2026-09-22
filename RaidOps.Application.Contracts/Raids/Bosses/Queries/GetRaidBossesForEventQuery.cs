using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Bosses.Responses;

namespace RaidOps.Application.Contracts.Raids.Bosses.Queries;

/// <summary>
/// Returns every boss encounter of the zone(s) a raid event targets — backs the boss navigation
/// strip on the event's Assignments page.
/// </summary>
public class GetRaidBossesForEventQuery : IQueryRequest<List<RaidBossResponse>>
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
