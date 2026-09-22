using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>Re-numbers a board's pages to match the given order.</summary>
public class ReorderRaidPlanPagesCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The board whose pages are being reordered. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanId { get; set; }

    /// <summary>The page IDs in their new display order.</summary>
    public required List<int> OrderedIds { get; set; }
}
