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
/// recurring patterns and resolving them into a day-by-day calendar for the requested range.
/// </summary>
public class GetMyAvailabilityQueryHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAvailabilityResolutionService availabilityResolutionService)
    : IQueryHandlerAsync<GetMyAvailabilityQuery, AvailabilityCalendarResponse>
{
    /// <inheritdoc/>
    public async Task<Result<AvailabilityCalendarResponse>> HandleAsync(GetMyAvailabilityQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<AvailabilityCalendarResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        if (query.RangeEnd < query.RangeStart)
            return Result<AvailabilityCalendarResponse>.Fail(ResponseDetail.InvalidRequest, "RangeEnd must be on or after RangeStart.");

        var exceptions = await availabilityRepository.GetExceptionsOverlappingAsync(
            query.RequesterDiscordId, query.GuildId, query.RangeStart, query.RangeEnd, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);

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
