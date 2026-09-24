using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Attributions.Commands;

/// <summary>Updates an existing row of a guild branch's raid-attribution template.</summary>
public class UpdateGuildAttributionDefinitionCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch the row belongs to. Set by the controller from the route.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the definition being updated. Set by the controller from the route.</summary>
    public int DefinitionId { get; set; }

    /// <summary>Display label.</summary>
    public required string Label { get; set; }

    /// <summary>Free-text grouping label, or <c>null</c> for an ungrouped row.</summary>
    public string? Section { get; set; }

    /// <summary>Whether officers can add a variable number of instances of this row per raid event.</summary>
    public bool IsRepeatable { get; set; }

    /// <summary>The row's full replacement set of ordered cells. Must contain at least one.</summary>
    public required List<AttributionCellRequest> Cells { get; set; }
}
