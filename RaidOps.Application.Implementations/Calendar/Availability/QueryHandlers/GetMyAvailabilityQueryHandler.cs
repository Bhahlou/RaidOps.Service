using RaidOps.Application.Contracts.Calendar.Availability.Queries;
using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.QueryHandlers;

/// <summary>
/// Handles <see cref="GetMyAvailabilityQuery"/> by loading the requester's exceptions and
/// recurring patterns across every scope and resolving them into a day-by-day personal overview for
/// the requested range. Purely self-scoped — no guild access check applies, since a member's own
/// declarations (whichever scope they belong to) are always theirs to read.
/// </summary>
public class GetMyAvailabilityQueryHandler(
    IAvailabilityRepository availabilityRepository,
    IAvailabilityResolutionService availabilityResolutionService)
    : IQueryHandlerAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>
{
    /// <inheritdoc/>
    public async Task<Result<AvailabilityCalendarResponse>> HandleAsync(GetMyAvailabilityQuery query, CancellationToken cancellationToken)
    {
        if (query.RangeEnd < query.RangeStart)
            return Result<AvailabilityCalendarResponse>.Fail(ResponseDetail.InvalidRequest, "RangeEnd must be on or after RangeStart.");

        var exceptions = await availabilityRepository.GetExceptionsOverlappingAsync(
            query.RequesterDiscordId, query.RangeStart, query.RangeEnd, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsAsync(query.RequesterDiscordId, cancellationToken);

        var resolvedDays = availabilityResolutionService.Resolve(query.RangeStart, query.RangeEnd, exceptions, patterns);

        return Result<AvailabilityCalendarResponse>.Ok(new AvailabilityCalendarResponse
        {
            Days = resolvedDays,
            Exceptions = [.. exceptions.Select(MapException)],
            // Only the current, still-open version of each pattern is editable — historical
            // (closed) versions exist purely so past resolved days stay stable and aren't surfaced here.
            Patterns = [.. patterns.Where(p => p.EffectiveUntil == null).Select(MapPattern)],
        });
    }

    private static AvailabilityExceptionResponse MapException(AvailabilityDeclaration exception) => new()
    {
        Id = exception.Id,
        GuildId = exception.GuildId,
        GuildBranchId = exception.GuildBranchId,
        StartDate = exception.StartDate,
        EndDate = exception.EndDate,
        Status = exception.Status,
        Reason = exception.Reason,
        AvailableFrom = exception.AvailableFrom,
        AvailableUntil = exception.AvailableUntil,
    };

    private static RecurringAvailabilityPatternResponse MapPattern(RecurringAvailabilityPattern pattern) => new()
    {
        Id = pattern.Id,
        GuildId = pattern.GuildId,
        GuildBranchId = pattern.GuildBranchId,
        Label = pattern.Label,
        CycleLengthDays = pattern.CycleLengthDays,
        AnchorDate = pattern.AnchorDate,
        Days = [.. pattern.Days.Select(MapPatternDay)],
    };

    private static RecurringAvailabilityPatternDayResponse MapPatternDay(RecurringAvailabilityPatternDay day) => new()
    {
        OffsetInCycle = day.OffsetInCycle,
        Status = day.Status,
        Reason = day.Reason,
        AvailableFrom = day.AvailableFrom,
        AvailableUntil = day.AvailableUntil,
    };
}
