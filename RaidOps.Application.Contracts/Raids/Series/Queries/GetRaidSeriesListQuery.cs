using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Series.Responses;

namespace RaidOps.Application.Contracts.Raids.Series.Queries;

/// <summary>
/// Returns every recurring raid template (active or not) belonging to a guild. The requesting
/// user must hold at least <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>.
/// </summary>
public class GetRaidSeriesListQuery : IQueryRequest<List<RaidSeriesResponse>>
{
    /// <summary>Discord snowflake ID of the guild whose series to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch whose series to retrieve.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
