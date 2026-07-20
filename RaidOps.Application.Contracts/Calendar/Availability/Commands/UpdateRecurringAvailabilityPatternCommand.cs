using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that replaces a recurring availability pattern's settings and full day set, effective
/// from today onward. Non-retroactive: the previous version stays exactly as it was for any date
/// before today (a new pattern version is inserted rather than mutating the existing one), so past
/// resolved days are never rewritten by this command.
/// </summary>
public class UpdateRecurringAvailabilityPatternCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild this pattern applies to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the member this pattern belongs to. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The pattern to update. Set by the controller from the route, not from the request body.</summary>
    public int PatternId { get; set; }

    /// <summary>Optional friendly name for the member's own reference.</summary>
    public string? Label { get; set; }

    /// <summary>Length of the recurrence cycle in days.</summary>
    public required int CycleLengthDays { get; set; }

    /// <summary>Reference date at which offset 0 of the cycle begins.</summary>
    public required DateOnly AnchorDate { get; set; }

    /// <summary>The days within the cycle that are not fully available, replacing the previous set entirely.</summary>
    public required List<RecurringAvailabilityPatternDayInput> Days { get; set; }
}
