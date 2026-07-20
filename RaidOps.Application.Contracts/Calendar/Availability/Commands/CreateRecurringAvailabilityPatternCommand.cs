using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that creates a recurring availability pattern (e.g. a weekly recurrence, or a shift rotation).
/// </summary>
public class CreateRecurringAvailabilityPatternCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild this pattern applies to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the member this pattern belongs to. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Optional friendly name for the member's own reference.</summary>
    public string? Label { get; set; }

    /// <summary>Length of the recurrence cycle in days (7 for a weekly pattern, or any other length for a shift rotation).</summary>
    public required int CycleLengthDays { get; set; }

    /// <summary>Reference date at which offset 0 of the cycle begins.</summary>
    public required DateOnly AnchorDate { get; set; }

    /// <summary>The days within the cycle that are not fully available.</summary>
    public required List<RecurringAvailabilityPatternDayInput> Days { get; set; }
}
