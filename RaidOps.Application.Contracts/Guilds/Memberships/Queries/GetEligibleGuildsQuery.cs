using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Queries;

/// <summary>
/// Query that returns the registered guilds a character is eligible to join
/// (Discord member, guild configured, roster access granted, not already a member).
/// </summary>
public class GetEligibleGuildsQuery : IQueryRequest<List<EligibleGuildResponse>>
{
    /// <summary>Internal ID of the character.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
