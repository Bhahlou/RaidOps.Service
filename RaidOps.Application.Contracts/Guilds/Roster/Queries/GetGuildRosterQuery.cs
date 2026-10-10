using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Roster.Responses;

namespace RaidOps.Application.Contracts.Guilds.Roster.Queries;

/// <summary>
/// Returns every active character on a registered guild's roster.
/// The requesting user must hold at least <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access.
/// </summary>
public class GetGuildRosterQuery : IQueryRequest<List<GuildRosterMemberResponse>>
{
    /// <summary>Discord snowflake ID of the guild whose roster to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the specific guild branch whose roster to retrieve.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the user requesting the roster.</summary>
    public required string RequesterDiscordId { get; set; }
}
