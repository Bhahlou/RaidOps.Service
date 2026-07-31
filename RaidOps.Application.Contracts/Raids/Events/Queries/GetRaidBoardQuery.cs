using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Responses;

namespace RaidOps.Application.Contracts.Raids.Events.Queries;

/// <summary>
/// Returns every raid event of a guild starting within a date range, with their target zones and
/// slot assignments. Does not materialize series occurrences itself — the caller is expected to
/// run <c>MaterializeRaidSeriesOccurrencesCommand</c> for the same range first (the front-end raid
/// board does this automatically before loading). The requesting user must hold at least
/// <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>.
/// </summary>
public class GetRaidBoardQuery : IQueryRequest<RaidBoardResponse>
{
    /// <summary>Discord snowflake ID of the guild whose board to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch whose board to retrieve.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>First local date (inclusive) to return events for.</summary>
    public required DateOnly RangeStart { get; set; }

    /// <summary>Last local date (inclusive) to return events for.</summary>
    public required DateOnly RangeEnd { get; set; }
}
