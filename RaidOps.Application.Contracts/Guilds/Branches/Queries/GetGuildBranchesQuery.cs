using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Responses;

namespace RaidOps.Application.Contracts.Guilds.Branches.Queries;

/// <summary>
/// Returns every WoW branch activated on a guild (active and deactivated), for the guild's
/// branches settings tab. The requesting user must be an admin of the guild.
/// </summary>
public class GetGuildBranchesQuery : IQueryRequest<List<GuildBranchResponse>>
{
    /// <summary>Discord snowflake ID of the guild whose branches to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
