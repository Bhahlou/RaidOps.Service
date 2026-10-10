using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that clears a single day out of one of the requesting member's own one-off
/// availability exceptions — shrinking it from either edge, splitting it in two if the day falls
/// in the middle, or deleting it outright if it was the only day covered. Only the member who
/// declared the exception may edit it.
/// </summary>
public class RemoveAvailabilityExceptionDayCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the requesting member. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The exception to edit. Set by the controller, not from the request body.</summary>
    public int ExceptionId { get; set; }

    /// <summary>The single date to clear back to Available. Must fall within the exception's current range.</summary>
    public required DateOnly Date { get; set; }
}
