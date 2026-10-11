using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Authentication.Commands;

/// <summary>
/// Command that records a guided tour the user has just finished or skipped, so the tour isn't
/// replayed on their other devices.
/// </summary>
public class MarkTourSeenCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the user acknowledging the tour. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The id of the tour the user has now seen (e.g. "general", "admin").</summary>
    public required string TourId { get; set; }
}
