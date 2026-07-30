using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that replaces the dates/status of one of the requesting member's own one-off
/// availability exceptions. Only the member who declared it may edit it.
/// </summary>
public class UpdateAvailabilityExceptionCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the requesting member. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The exception to edit. Set by the controller, not from the request body.</summary>
    public int ExceptionId { get; set; }

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
