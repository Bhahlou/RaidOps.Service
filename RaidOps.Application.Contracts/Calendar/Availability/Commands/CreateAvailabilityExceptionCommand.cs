using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that declares a one-off availability exception for a single date or date range.
/// Always takes precedence over any recurring pattern covering the same scope's dates.
/// </summary>
public class CreateAvailabilityExceptionCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the requesting member. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The guild of the target branch scope, or <c>null</c> for a Global declaration. Set together with <see cref="GuildBranchId"/>.</summary>
    public string? GuildId { get; set; }

    /// <summary>The specific branch scope, or <c>null</c> for a Global declaration. Set together with <see cref="GuildId"/>.</summary>
    public int? GuildBranchId { get; set; }

    /// <summary>First date covered by this exception (inclusive).</summary>
    public required DateOnly StartDate { get; set; }

    /// <summary>Last date covered by this exception (inclusive). Equal to <see cref="StartDate"/> for a single day.</summary>
    public required DateOnly EndDate { get; set; }

    /// <summary>Declared status for every date in the range.</summary>
    public required DayAvailabilityStatus Status { get; set; }

    /// <summary>Optional free-text reason.</summary>
    public string? Reason { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available.</summary>
    public TimeOnly? AvailableUntil { get; set; }
}
