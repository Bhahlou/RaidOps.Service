using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>Permanently deletes a strategy board, cascading its pages and elements.</summary>
public class DeleteRaidPlanCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The board to delete. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanId { get; set; }
}
