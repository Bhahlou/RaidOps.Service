using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Attributions.Commands;

/// <summary>Clears one slot of a raid event's attributions.</summary>
public class ClearRaidEventAttributionCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch the event belongs to. Set by the controller from the route.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Surrogate ID of the raid event. Set by the controller from the route.</summary>
    public int EventId { get; set; }

    /// <summary>FK to the guild's template row being cleared.</summary>
    public required int DefinitionId { get; set; }

    /// <summary>FK to the name-slot cell being cleared — must belong to <see cref="DefinitionId"/>.</summary>
    public required int CellId { get; set; }

    /// <summary>0-based instance number to clear, for a repeatable row. Always 0 for a non-repeatable row.</summary>
    public int InstanceIndex { get; set; }
}
