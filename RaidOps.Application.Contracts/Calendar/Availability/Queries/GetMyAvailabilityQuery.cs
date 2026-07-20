using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Calendar.Availability.Queries;

/// <summary>
/// Query that returns the requesting member's resolved availability over a date range for a
/// specific guild, along with the raw exceptions and recurring patterns backing it (for editing).
/// </summary>
public class GetMyAvailabilityQuery : IQueryRequest<AvailabilityCalendarResponse>
{
    /// <summary>The Discord snowflake ID of the guild to resolve availability for.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the requesting member.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>First date to resolve (inclusive).</summary>
    public required DateOnly RangeStart { get; set; }

    /// <summary>Last date to resolve (inclusive).</summary>
    public required DateOnly RangeEnd { get; set; }
}
