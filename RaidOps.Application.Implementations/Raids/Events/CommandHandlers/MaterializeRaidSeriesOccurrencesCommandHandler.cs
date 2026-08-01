using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="MaterializeRaidSeriesOccurrencesCommand"/> by walking every active
/// <see cref="RaidSeries"/> of the guild day-by-day over the requested range, creating a
/// <see cref="RaidEvent"/> for each date matching the series' recurrence day/interval that hasn't
/// already been materialized. Safe to call repeatedly for overlapping ranges.
/// </summary>
public class MaterializeRaidSeriesOccurrencesCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IRaidSeriesRepository raidSeriesRepository,
    IRaidEventRepository raidEventRepository) : ICommandHandlerAsync<MaterializeRaidSeriesOccurrencesCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(MaterializeRaidSeriesOccurrencesCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        if (command.RangeEnd < command.RangeStart)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "RangeEnd must be on or after RangeStart.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        var activeSeries = (await raidSeriesRepository.GetByGuildBranchIdAsync(command.GuildBranchId, cancellationToken))
            .Where(s => s.IsActive)
            .ToList();

        var materializedCount = 0;
        foreach (var series in activeSeries)
        {
            for (var date = command.RangeStart; date <= command.RangeEnd; date = date.AddDays(1))
            {
                if (date.DayOfWeek != series.RecurrenceDayOfWeek)
                    continue;

                if (!IsOccurrenceWeek(date, DateOnly.FromDateTime(series.CreatedAt), series.RecurrenceIntervalWeeks))
                    continue;

                var localStart = date.ToDateTime(series.RecurrenceStartTimeLocal);
                var startsAtUtc = GuildTimeHelper.FromGuildLocal(localStart, guild.Timezone);

                if (await raidEventRepository.ExistsForSeriesAndDateAsync(series.Id, startsAtUtc, cancellationToken))
                    continue;

                // PublicationStatus is left unset here, relying on RaidEvent's own Draft default —
                // materialized occurrences are never auto-published, only PublishRaidEventCommand can do that.
                var raidEvent = new RaidEvent
                {
                    GuildId = command.GuildId,
                    GuildBranchId = command.GuildBranchId,
                    RaidSeriesId = series.Id,
                    Name = series.Name,
                    StartsAtUtc = startsAtUtc,
                    GroupCount = series.GroupCount,
                    SlotsPerGroup = series.SlotsPerGroup,
                    SignupMode = series.SignupMode,
                    Status = RaidEventStatus.Scheduled,
                    CreatedByDiscordId = command.RequesterDiscordId,
                    CreatedAt = DateTime.UtcNow,
                    TargetZones = [.. series.DefaultZones.Select(z => new RaidEventZone { RaidZoneId = z.RaidZoneId })],
                };

                await raidEventRepository.AddAsync(raidEvent, cancellationToken);
                materializedCount++;
            }
        }

        return Result<CommandResponse>.Ok(new CommandResponse($"Materialized {materializedCount} occurrence(s).", new { materializedCount }));
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="candidateDate"/>'s week falls on the recurrence
    /// cadence relative to <paramref name="anchorDate"/>'s week (e.g. every other week for a
    /// bi-weekly series). <see cref="RaidSeries"/> has no dedicated recurrence anchor field, so
    /// the series' creation date is used as the reference week.
    /// </summary>
    private static bool IsOccurrenceWeek(DateOnly candidateDate, DateOnly anchorDate, int intervalWeeks)
    {
        if (intervalWeeks <= 1)
            return true;

        var weeksBetween = (StartOfWeek(candidateDate).DayNumber - StartOfWeek(anchorDate).DayNumber) / 7;
        return ((weeksBetween % intervalWeeks) + intervalWeeks) % intervalWeeks == 0;
    }

    /// <summary>Returns the Monday of the ISO week containing <paramref name="date"/>.</summary>
    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // Monday = 0 ... Sunday = 6
        return date.AddDays(-offset);
    }
}
