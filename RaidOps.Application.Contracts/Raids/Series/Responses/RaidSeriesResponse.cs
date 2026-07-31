using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Series.Responses;

/// <summary>A recurring raid template. Returned by <c>GetRaidSeriesListQuery</c>.</summary>
public class RaidSeriesResponse
{
    /// <summary>Internal series ID.</summary>
    public required int Id { get; set; }

    /// <summary>Display name (e.g. "Split 1").</summary>
    public required string Name { get; set; }

    /// <summary>FK to the WoW game-version branch this series targets, resolved via its guild branch.</summary>
    public required int BranchId { get; set; }

    /// <summary>Display name of the WoW game-version branch.</summary>
    public required string BranchName { get; set; }

    /// <summary>Day of the week each occurrence falls on.</summary>
    public required DayOfWeek RecurrenceDayOfWeek { get; set; }

    /// <summary>Start time of each occurrence, local to the guild's timezone.</summary>
    public required TimeOnly RecurrenceStartTimeLocal { get; set; }

    /// <summary>Number of weeks between occurrences (1 = weekly, 2 = bi-weekly, …).</summary>
    public required int RecurrenceIntervalWeeks { get; set; }

    /// <summary>Number of groups in the raid grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>How attendance is determined for occurrences of this series.</summary>
    public required SignupMode SignupMode { get; set; }

    /// <summary>Whether this series is still active (materializing future occurrences).</summary>
    public required bool IsActive { get; set; }

    /// <summary>The raid zones every materialized occurrence targets by default.</summary>
    public required List<RaidZoneRefResponse> RaidZones { get; set; }
}
