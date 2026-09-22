using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>Adds a new page to a board, appended after its current last page.</summary>
public class CreateRaidPlanPageCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The board to add a page to. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanId { get; set; }

    /// <summary>Display name (e.g. "Phase 2").</summary>
    public required string Name { get; set; }
}
