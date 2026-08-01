using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Events.Commands;

/// <summary>
/// Updates a raid event's schedule and target-zone set (works for both ad-hoc events and
/// series-materialized occurrences — editing an occurrence never mutates its parent series). The
/// requesting user must hold <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on
/// <see cref="GuildId"/>.
/// </summary>
public class UpdateRaidEventCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer updating this event. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>ID of the event to update. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }

    /// <summary>Surrogate ID of the guild branch this event targets. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>UTC timestamp this event starts at.</summary>
    public required DateTime StartsAtUtc { get; set; }

    /// <summary>Number of groups in the raid grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>IDs of the raid zones this event targets. Must contain at least one zone.</summary>
    public required List<int> RaidZoneIds { get; set; }
}
