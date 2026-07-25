using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Roster.Responses;

namespace RaidOps.Application.Contracts.Raids.Roster.Queries;

/// <summary>
/// Returns every active roster character not assigned to any non-cancelled raid event starting
/// within a date range. The requesting user must hold at least
/// <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on <see cref="GuildId"/>.
/// </summary>
public class GetUnassignedGuildMembersQuery : IQueryRequest<List<UnassignedMemberResponse>>
{
    /// <summary>Discord snowflake ID of the guild whose roster to check.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>First local date (inclusive) of the range to check assignments over.</summary>
    public required DateOnly RangeStart { get; set; }

    /// <summary>Last local date (inclusive) of the range to check assignments over.</summary>
    public required DateOnly RangeEnd { get; set; }
}
