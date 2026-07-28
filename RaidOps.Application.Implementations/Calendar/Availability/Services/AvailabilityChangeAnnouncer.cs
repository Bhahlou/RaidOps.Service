using RaidOps.Application.Contracts.Calendar.Availability.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Calendar.Availability.Services;

/// <inheritdoc cref="IAvailabilityChangeAnnouncer"/>
public class AvailabilityChangeAnnouncer(
    IAvailabilityResolutionService resolutionService,
    IAuditLogService auditLogService,
    IGuildNotificationDispatcher guildNotificationDispatcher,
    IAbsenceNotificationContentBuilder absenceNotificationContentBuilder) : IAvailabilityChangeAnnouncer
{
    private sealed record DeltaSegment(DateOnly Start, DateOnly End, bool IsAdded, DayAvailabilityStatus Status, TimeOnly? AvailableFrom, TimeOnly? AvailableUntil);

    /// <inheritdoc/>
    public async Task AnnounceAsync(AvailabilityChange change, CancellationToken cancellationToken = default)
    {
        var (guildId, guildBranchId, requesterDiscordId, windowStart, windowEnd, beforeExceptions, afterExceptions, patterns) = change;

        // A Global mutation has no single guild to audit-log or notify against — properly announcing
        // it means fanning out across every branch where the member has an active roster character
        // (calendar global rework Phase C, not implemented yet). Silently unannounced until then.
        if (guildId == null)
            return;

        var before = resolutionService.ResolveForScope(windowStart, windowEnd, beforeExceptions, patterns, guildId, guildBranchId);
        var after = resolutionService.ResolveForScope(windowStart, windowEnd, afterExceptions, patterns, guildId, guildBranchId);

        var segments = BuildDeltaSegments(before, after);
        if (segments.Count == 0)
            return;

        var language = await absenceNotificationContentBuilder.GetGuildLanguageAsync(guildId, cancellationToken);

        foreach (var segment in segments)
        {
            var action = segment.IsAdded ? GuildAuditAction.AvailabilityExceptionDeclared : GuildAuditAction.AvailabilityExceptionDeleted;

            await auditLogService.LogAsync(
                guildId,
                requesterDiscordId,
                action,
                new Dictionary<string, string>
                {
                    ["startDate"] = segment.Start.ToString("yyyy-MM-dd"),
                    ["endDate"] = segment.End.ToString("yyyy-MM-dd"),
                    ["status"] = segment.Status.ToString(),
                    ["availableFrom"] = segment.AvailableFrom?.ToString("HH:mm:ss") ?? string.Empty,
                    ["availableUntil"] = segment.AvailableUntil?.ToString("HH:mm:ss") ?? string.Empty,
                },
                cancellationToken);

            var eventType = segment.IsAdded ? GuildNotificationEventType.AbsenceAdded : GuildNotificationEventType.AbsenceRemoved;
            var kind = AbsenceNotificationText.DetermineKind(segment.Status, segment.AvailableFrom, segment.AvailableUntil);

            var dateRange = AbsenceNotificationText.FormatDateRange(segment.Start, segment.End, language);
            var partialSuffix = AbsenceNotificationText.FormatPartialSuffix(kind, segment.AvailableFrom, segment.AvailableUntil, language);
            var datesValue = partialSuffix is null ? dateRange : $"{dateRange} · {partialSuffix}";

            var embed = await absenceNotificationContentBuilder.BuildAsync(
                guildId,
                requesterDiscordId,
                eventType,
                kind,
                [new DiscordEmbedField("Dates", datesValue)],
                cancellationToken);

            await guildNotificationDispatcher.NotifyAsync(guildId, eventType, embed, cancellationToken);
        }
    }

    /// <summary>
    /// Folds the day-by-day before/after resolution into contiguous runs of days that flipped
    /// between "restricted" (Absent/Partial) and "available" — the unit an admin actually cares
    /// about, not the individual DB rows that produced it. Each segment carries the status of
    /// whichever side of the flip is the "interesting" one: the new status for an added segment,
    /// the status that was just lifted for a removed one — mirroring what the previous
    /// per-command audit logging captured, so the front's existing rendering needs no changes.
    /// </summary>
    private static List<DeltaSegment> BuildDeltaSegments(
        List<ResolvedDayAvailabilityResponse> before,
        List<ResolvedDayAvailabilityResponse> after)
    {
        var beforeByDate = before.ToDictionary(d => d.Date);
        var segments = new List<DeltaSegment>();
        DeltaSegment? current = null;

        foreach (var day in after.OrderBy(d => d.Date))
        {
            var beforeDay = beforeByDate[day.Date];
            var wasRestricted = IsRestricted(beforeDay.Status);
            var isRestricted = IsRestricted(day.Status);

            if (wasRestricted == isRestricted)
            {
                FlushCurrent();
                continue;
            }

            var isAdded = isRestricted;
            var reference = isAdded ? day : beforeDay;

            // No day.Date == c.End.AddDays(1) check needed: Resolve() always returns one entry per
            // calendar day with no gaps, so whenever `current` survives to here it was set on the
            // immediately preceding iteration — day.Date is always c.End.AddDays(1) by construction.
            if (current is { } c && c.IsAdded == isAdded
                && c.Status == reference.Status && c.AvailableFrom == reference.AvailableFrom && c.AvailableUntil == reference.AvailableUntil)
                current = c with { End = day.Date };
            else
            {
                FlushCurrent();
                current = new DeltaSegment(day.Date, day.Date, isAdded, reference.Status, reference.AvailableFrom, reference.AvailableUntil);
            }
        }

        FlushCurrent();
        return segments;

        void FlushCurrent()
        {
            if (current is { } c)
                segments.Add(c);
            current = null;
        }
    }

    private static bool IsRestricted(DayAvailabilityStatus status) => status != DayAvailabilityStatus.Available;
}
