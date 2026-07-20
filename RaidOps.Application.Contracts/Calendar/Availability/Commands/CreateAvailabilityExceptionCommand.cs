using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that declares a one-off availability exception for a single date or date range.
/// Always takes precedence over any recurring pattern covering the same dates.
/// </summary>
public class CreateAvailabilityExceptionCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild this exception applies to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the member declaring this exception. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

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
