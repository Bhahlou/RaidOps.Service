using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;

namespace RaidOps.Application.Implementations.Calendar.Availability.Services;

/// <summary>
/// Default <see cref="IAvailabilityResolutionService"/> implementation.
/// </summary>
public class AvailabilityResolutionService : IAvailabilityResolutionService
{
    /// <inheritdoc/>
    public List<ResolvedDayAvailabilityResponse> Resolve(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        IReadOnlyCollection<AvailabilityDeclaration> exceptions,
        IReadOnlyCollection<RecurringAvailabilityPattern> patterns)
    {
        var results = new List<ResolvedDayAvailabilityResponse>();

        for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
        {
            var exception = exceptions.FirstOrDefault(e => date >= e.StartDate && date <= e.EndDate);
            results.Add(exception != null
                ? ResolveFromException(date, exception)
                : ResolveFromPatterns(date, patterns));
        }

        return results;
    }

    private static ResolvedDayAvailabilityResponse ResolveFromException(DateOnly date, AvailabilityDeclaration exception) => new()
    {
        Date = date,
        Status = exception.Status,
        Reason = exception.Reason,
        AvailableFrom = exception.AvailableFrom,
        AvailableUntil = exception.AvailableUntil,
        IsException = true,
    };

    private static ResolvedDayAvailabilityResponse ResolveFromPatterns(DateOnly date, IReadOnlyCollection<RecurringAvailabilityPattern> patterns)
    {
        RecurringAvailabilityPatternDay? mostRestrictive = null;

        foreach (var pattern in patterns)
        {
            if (date < pattern.EffectiveFrom || (pattern.EffectiveUntil.HasValue && date > pattern.EffectiveUntil.Value))
                continue;

            var offset = OffsetInCycle(date, pattern.AnchorDate, pattern.CycleLengthDays);
            var day = pattern.Days.FirstOrDefault(d => d.OffsetInCycle == offset);
            if (day != null && (mostRestrictive == null || Restrictiveness(day.Status) > Restrictiveness(mostRestrictive.Status)))
                mostRestrictive = day;
        }

        return mostRestrictive == null
            ? new ResolvedDayAvailabilityResponse { Date = date, Status = DayAvailabilityStatus.Available }
            : new ResolvedDayAvailabilityResponse
            {
                Date = date,
                Status = mostRestrictive.Status,
                Reason = mostRestrictive.Reason,
                AvailableFrom = mostRestrictive.AvailableFrom,
                AvailableUntil = mostRestrictive.AvailableUntil,
                IsException = false,
            };
    }

    /// <summary>Zero-based offset of <paramref name="date"/> within a cycle of <paramref name="cycleLengthDays"/> anchored at <paramref name="anchorDate"/>.</summary>
    private static int OffsetInCycle(DateOnly date, DateOnly anchorDate, int cycleLengthDays)
    {
        var offset = (date.DayNumber - anchorDate.DayNumber) % cycleLengthDays;
        return offset < 0 ? offset + cycleLengthDays : offset;
    }

    private static int Restrictiveness(DayAvailabilityStatus status) => status switch
    {
        DayAvailabilityStatus.Absent => 2,
        DayAvailabilityStatus.Partial => 1,
        _ => 0,
    };
}
