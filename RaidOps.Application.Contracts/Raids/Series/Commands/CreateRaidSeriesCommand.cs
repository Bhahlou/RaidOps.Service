using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Series.Commands;

/// <summary>
/// Creates a new recurring raid template. The requesting user must hold
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class CreateRaidSeriesCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this series belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer creating this series. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Display name (e.g. "Split 1").</summary>
    public required string Name { get; set; }

    /// <summary>FK to the game branch this series targets.</summary>
    public required int BranchId { get; set; }

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

    /// <summary>IDs of the raid zones every materialized occurrence targets by default. Must contain at least one zone.</summary>
    public required List<int> RaidZoneIds { get; set; }
}
