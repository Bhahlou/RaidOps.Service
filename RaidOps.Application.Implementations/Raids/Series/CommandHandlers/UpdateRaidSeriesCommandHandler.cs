using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Series.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Series.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRaidSeriesCommand"/> by verifying officer access, validating the
/// requested grid shape and target zones, then replacing the template's scalar fields and
/// default-zone set — never touches occurrences already materialized from it.
/// </summary>
public class UpdateRaidSeriesCommandHandler(
    IRaidGridAndZoneValidator gridAndZoneValidator,
    IRaidSeriesRepository raidSeriesRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateRaidSeriesCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRaidSeriesCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await gridAndZoneValidator.ValidateAsync(
            command.RequesterDiscordId, command.GuildId, command.GuildBranchId, command.GroupCount, command.SlotsPerGroup, command.RaidZoneIds, cancellationToken);
        if (validation.IsFailed)
            return Result<CommandResponse>.Fail(validation.Error!, validation.Detail);

        var distinctZoneIds = validation.Value!;

        var series = new RaidSeries
        {
            Id = command.SeriesId,
            Name = command.Name,
            RecurrenceDayOfWeek = command.RecurrenceDayOfWeek,
            RecurrenceStartTimeLocal = command.RecurrenceStartTimeLocal,
            RecurrenceIntervalWeeks = command.RecurrenceIntervalWeeks <= 0 ? 1 : command.RecurrenceIntervalWeeks,
            GroupCount = command.GroupCount,
            SlotsPerGroup = command.SlotsPerGroup,
        };

        var updated = await raidSeriesRepository.UpdateAsync(series, command.GuildBranchId, distinctZoneIds, cancellationToken);
        if (!updated)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidSeriesNotFound, $"Raid series '{command.SeriesId}' does not exist.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidSeriesUpdated,
            new Dictionary<string, string> { ["seriesName"] = command.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid series updated successfully."));
    }
}
