using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Series.Commands;

/// <summary>
/// Replaces the settings and default-zone set of an existing recurring raid template — never
/// touches occurrences already materialized from it. The requesting user must hold
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class UpdateRaidSeriesCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this series belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer updating this series. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>ID of the series to update. Set by the controller from the route, not from the request body.</summary>
    public int SeriesId { get; set; }

    /// <summary>Surrogate ID of the guild branch this series targets. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Display name (e.g. "Split 1").</summary>
    public required string Name { get; set; }

    /// <summary>Day of the week each occurrence falls on.</summary>
    public required DayOfWeek RecurrenceDayOfWeek { get; set; }

    /// <summary>Start time of each occurrence, local to the guild's timezone.</summary>
    public required TimeOnly RecurrenceStartTimeLocal { get; set; }

    /// <summary>Number of weeks between occurrences (1 = weekly, 2 = bi-weekly, …).</summary>
    public int RecurrenceIntervalWeeks { get; set; } = 1;

    /// <summary>Number of groups in the raid grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>IDs of the raid zones every future materialized occurrence targets by default. Must contain at least one zone.</summary>
    public required List<int> RaidZoneIds { get; set; }
}
