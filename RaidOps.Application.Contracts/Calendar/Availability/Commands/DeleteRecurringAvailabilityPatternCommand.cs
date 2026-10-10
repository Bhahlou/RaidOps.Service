using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that deletes a recurring availability pattern. Only the member who owns it may delete it.
/// </summary>
public class DeleteRecurringAvailabilityPatternCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the requesting member. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The pattern to delete.</summary>
    public required int PatternId { get; set; }
}
