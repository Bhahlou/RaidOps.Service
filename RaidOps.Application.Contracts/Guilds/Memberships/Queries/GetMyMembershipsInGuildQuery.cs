using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Queries;

/// <summary>
/// Query that returns all of the requesting user's characters that are on a specific guild's roster.
/// </summary>
public class GetMyMembershipsInGuildQuery : IQueryRequest<List<CharacterInGuildResponse>>
{
    /// <summary>Discord snowflake ID of the guild.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
