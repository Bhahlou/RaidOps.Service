using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Attributions.Commands;

/// <summary>Adds a new row to the guild's raid-attribution template, appended at the end.</summary>
public class CreateGuildAttributionDefinitionCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Display label.</summary>
    public required string Label { get; set; }

    /// <summary>Free-text grouping label, or <c>null</c> for an ungrouped row.</summary>
    public string? Section { get; set; }

    /// <summary>Whether officers can add a variable number of instances of this row per raid event.</summary>
    public bool IsRepeatable { get; set; }

    /// <summary>The row's ordered cells (icons and name slots). Must contain at least one.</summary>
    public required List<AttributionCellRequest> Cells { get; set; }
}
