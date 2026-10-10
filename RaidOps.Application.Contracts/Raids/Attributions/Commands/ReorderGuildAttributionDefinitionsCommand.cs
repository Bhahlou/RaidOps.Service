using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Attributions.Commands;

/// <summary>Re-numbers a guild branch's template rows to match the given order.</summary>
public class ReorderGuildAttributionDefinitionsCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch whose template is reordered. Set by the controller from the route.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Definition IDs in their new display order. IDs not belonging to the guild branch are ignored.</summary>
    public required List<int> OrderedIds { get; set; }
}
