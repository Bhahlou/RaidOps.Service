using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>
/// Replaces every element of a page with the given list — the one bulk mutation the canvas
/// editor's Save button fires. There is no per-element CRUD.
/// </summary>
public class SaveRaidPlanPageElementsCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The board the page belongs to. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanId { get; set; }

    /// <summary>The page whose elements are being saved. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanPageId { get; set; }

    /// <summary>The page's full new element list.</summary>
    public required List<RaidPlanElementRequest> Elements { get; set; }
}
