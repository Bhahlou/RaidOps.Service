using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Queries;

/// <summary>
/// Returns all registered guilds the authenticated user could add at least one of their
/// characters to, along with the specific characters eligible for each guild.
/// </summary>
public class GetEligibleGuildsBulkQuery : IQueryRequest<List<GuildEligibilityResponse>>
{
    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
