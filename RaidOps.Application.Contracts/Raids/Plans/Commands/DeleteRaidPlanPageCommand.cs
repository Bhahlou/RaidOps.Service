using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>Permanently deletes a page, cascading its elements.</summary>
public class DeleteRaidPlanPageCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The board the page belongs to. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanId { get; set; }

    /// <summary>The page to delete. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanPageId { get; set; }
}
