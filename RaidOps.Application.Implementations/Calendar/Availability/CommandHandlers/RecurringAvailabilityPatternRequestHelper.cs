using System.Text.Json;
using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Shared request handling for creating/updating a recurring availability pattern — the
/// cycle/offset validation, day-set mapping, and audit-log variable shape are identical whether
/// a pattern is being created outright or replaced with a new version, and were duplicated
/// verbatim across those two handlers (and partially a third, for stopping one) before this.
/// </summary>
internal static class RecurringAvailabilityPatternRequestHelper
{
    private const string TimeFormat = "HH:mm:ss";

    /// <summary>Validates the cycle length and every day offset against it. Returns the failure to return, or <c>null</c> if valid.</summary>
    public static Result<CommandResponse>? ValidateCycleAndDays(int cycleLengthDays, IReadOnlyCollection<RecurringAvailabilityPatternDayInput> days)
    {
        if (cycleLengthDays < 1)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "CycleLengthDays must be at least 1.");

        if (days.Any(d => d.OffsetInCycle < 0 || d.OffsetInCycle >= cycleLengthDays))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "Every day offset must be within the cycle length.");

        if (days.Any(d => d.Status == DayAvailabilityStatus.Partial && d.AvailableFrom == null && d.AvailableUntil == null))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "A Partial day needs at least one of AvailableFrom/AvailableUntil.");

        return null;
    }

    /// <summary>Maps submitted day inputs to persistable entities for a new pattern version.</summary>
    public static List<RecurringAvailabilityPatternDay> MapDays(IEnumerable<RecurringAvailabilityPatternDayInput> days) =>
        [.. days.Select(d => new RecurringAvailabilityPatternDay
        {
            OffsetInCycle = d.OffsetInCycle,
            Status = d.Status,
            Reason = d.Reason,
            AvailableFrom = d.AvailableFrom,
            AvailableUntil = d.AvailableUntil,
        })];

    /// <summary>Builds the audit-log <c>Details</c> variables for a created/updated pattern, from the submitted request.</summary>
    public static Dictionary<string, string> BuildAuditVariables(string? label, int cycleLengthDays, DateOnly anchorDate, IEnumerable<RecurringAvailabilityPatternDayInput> days) =>
        new()
        {
            ["label"] = label ?? string.Empty,
            ["cycleLengthDays"] = cycleLengthDays.ToString(),
            ["anchorDate"] = anchorDate.ToString("yyyy-MM-dd"),
            ["days"] = JsonSerializer.Serialize(days.Select(d => new
            {
                offsetInCycle = d.OffsetInCycle,
                status = d.Status.ToString(),
                availableFrom = d.AvailableFrom?.ToString(TimeFormat),
                availableUntil = d.AvailableUntil?.ToString(TimeFormat),
            })),
        };

    /// <summary>Builds the audit-log <c>Details</c> variables for a stopped pattern, from its persisted (not resubmitted) day set.</summary>
    public static Dictionary<string, string> BuildAuditVariables(string? label, int cycleLengthDays, DateOnly anchorDate, IEnumerable<RecurringAvailabilityPatternDay> days) =>
        new()
        {
            ["label"] = label ?? string.Empty,
            ["cycleLengthDays"] = cycleLengthDays.ToString(),
            ["anchorDate"] = anchorDate.ToString("yyyy-MM-dd"),
            ["days"] = JsonSerializer.Serialize(days.Select(d => new
            {
                offsetInCycle = d.OffsetInCycle,
                status = d.Status.ToString(),
                availableFrom = d.AvailableFrom?.ToString(TimeFormat),
                availableUntil = d.AvailableUntil?.ToString(TimeFormat),
            })),
        };
}
