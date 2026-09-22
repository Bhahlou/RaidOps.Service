using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Plans.Commands;

/// <summary>Sets or clears a page's background image.</summary>
public class SetRaidPlanPageBackgroundCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The board the page belongs to. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanId { get; set; }

    /// <summary>The page to set the background of. Set by the controller from the route, not from the request body.</summary>
    public int RaidPlanPageId { get; set; }

    /// <summary>Key into the front-end's bundled background-image manifest, or <c>null</c> to clear it.</summary>
    public string? BackgroundImageKey { get; set; }
}
