using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>A single raid event with its target zones and slot assignments.</summary>
public class RaidEventResponse
{
    /// <summary>Internal event ID.</summary>
    public required int Id { get; set; }

    /// <summary>FK to the series this occurrence was materialized from, or <c>null</c> for an ad-hoc event.</summary>
    public int? RaidSeriesId { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>FK to the game branch this event targets.</summary>
    public required int BranchId { get; set; }

    /// <summary>Display name of the game branch.</summary>
    public required string BranchName { get; set; }

    /// <summary>UTC timestamp this event starts at.</summary>
    public required DateTime StartsAtUtc { get; set; }

    /// <summary>Number of groups in the raid grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>How attendance is determined for this event.</summary>
    public required SignupMode SignupMode { get; set; }

    /// <summary>Lifecycle status of this event.</summary>
    public required RaidEventStatus Status { get; set; }

    /// <summary>Draft/published status of this event.</summary>
    public required RaidPublicationStatus PublicationStatus { get; set; }

    /// <summary>UTC timestamp this event was published at, or <c>null</c> while still a draft.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who published this event, or <c>null</c> while still a draft.</summary>
    public string? PublishedByDiscordId { get; set; }

    /// <summary>The raid zones this event targets.</summary>
    public required List<RaidZoneRefResponse> RaidZones { get; set; }

    /// <summary>The sparse slot assignments for this event.</summary>
    public required List<RaidSlotAssignmentResponse> Assignments { get; set; }
}
