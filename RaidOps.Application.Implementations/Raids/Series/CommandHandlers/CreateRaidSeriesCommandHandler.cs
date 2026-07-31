using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Series.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Series.CommandHandlers;

/// <summary>
/// Handles <see cref="CreateRaidSeriesCommand"/> by verifying officer access, validating the
/// requested grid shape and target zones, then persisting the new recurring template.
/// </summary>
public class CreateRaidSeriesCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidSeriesRepository raidSeriesRepository,
    IRaidZoneRepository raidZoneRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateRaidSeriesCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRaidSeriesCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        if (command.GroupCount <= 0 || command.SlotsPerGroup <= 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "GroupCount and SlotsPerGroup must be positive.");

        var distinctZoneIds = command.RaidZoneIds.Distinct().ToList();
        if (distinctZoneIds.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "At least one raid zone must be targeted.");

        var zones = await raidZoneRepository.GetByIdsAsync(distinctZoneIds, cancellationToken);
        if (zones.Count != distinctZoneIds.Count)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidZoneNotFound, "One or more raid zones do not exist.");

        var series = new RaidSeries
        {
            GuildId = command.GuildId,
            GuildBranchId = command.GuildBranchId,
            Name = command.Name,
            RecurrenceDayOfWeek = command.RecurrenceDayOfWeek,
            RecurrenceStartTimeLocal = command.RecurrenceStartTimeLocal,
            RecurrenceIntervalWeeks = command.RecurrenceIntervalWeeks <= 0 ? 1 : command.RecurrenceIntervalWeeks,
            GroupCount = command.GroupCount,
            SlotsPerGroup = command.SlotsPerGroup,
            SignupMode = SignupMode.DefaultPresent,
            IsActive = true,
            CreatedByDiscordId = command.RequesterDiscordId,
            CreatedAt = DateTime.UtcNow,
            DefaultZones = [.. distinctZoneIds.Select(id => new RaidSeriesZone { RaidZoneId = id })],
        };

        var created = await raidSeriesRepository.AddAsync(series, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidSeriesCreated,
            new Dictionary<string, string> { ["seriesName"] = command.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid series created successfully.", new { created.Id }));
    }
}
