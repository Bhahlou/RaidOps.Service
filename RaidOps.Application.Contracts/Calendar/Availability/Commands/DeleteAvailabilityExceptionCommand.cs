using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Calendar.Availability.Commands;

/// <summary>
/// Command that deletes a one-off availability exception. Only the member who declared it may delete it.
/// </summary>
public class DeleteAvailabilityExceptionCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild this exception applies to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the requesting member. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The exception to delete.</summary>
    public required int ExceptionId { get; set; }
}
