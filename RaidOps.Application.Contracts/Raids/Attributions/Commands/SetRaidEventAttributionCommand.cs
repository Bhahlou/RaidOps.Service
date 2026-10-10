using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Attributions.Commands;

/// <summary>Fills one slot of a raid event's attributions with a character already seated in that event.</summary>
public class SetRaidEventAttributionCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch the event belongs to. Set by the controller from the route.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Surrogate ID of the raid event. Set by the controller from the route.</summary>
    public int EventId { get; set; }

    /// <summary>Boss this fill belongs to, or <c>null</c> for the "General" (raid-wide) page. Must match the target definition's <c>RaidBossId</c>.</summary>
    public int? BossId { get; set; }

    /// <summary>FK to the guild's template row being filled.</summary>
    public required int DefinitionId { get; set; }

    /// <summary>FK to the name-slot cell being filled — must belong to <see cref="DefinitionId"/>.</summary>
    public required int CellId { get; set; }

    /// <summary>0-based instance number to fill, for a repeatable row. Must be 0 for a non-repeatable row.</summary>
    public int InstanceIndex { get; set; }

    /// <summary>
    /// FK to the character being assigned — must already have a slot assignment on this same
    /// event (i.e. be seated in it), checked by the handler.
    /// </summary>
    public required int CharacterId { get; set; }
}
