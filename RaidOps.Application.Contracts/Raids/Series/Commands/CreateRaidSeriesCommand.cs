using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

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

    /// <summary>IDs of the raid zones every materialized occurrence targets by default. Must contain at least one zone.</summary>
    public required List<int> RaidZoneIds { get; set; }

    /// <summary>Overrides the guild branch's default <see cref="SignupMode"/> for this series. <c>null</c> means "use the branch default."</summary>
    public SignupMode? SignupModeOverride { get; set; }

    /// <summary>
    /// Discord snowflake ID of a dedicated channel every occurrence of this series should post its
    /// raid-related notifications to instead of the guild-wide configured channel. <c>null</c> means
    /// "use the guild-wide configured channel."
    /// </summary>
    public string? DedicatedAnnouncementChannelId { get; set; }

    /// <inheritdoc cref="Domain.Models.Raids.RaidSeries.DedicatedAnnouncementChannelCategoryId"/>
    public string? DedicatedAnnouncementChannelCategoryId { get; set; }
}
