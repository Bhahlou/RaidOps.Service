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
    IRaidGridAndZoneValidator gridAndZoneValidator,
    IRaidSeriesRepository raidSeriesRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateRaidSeriesCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRaidSeriesCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await gridAndZoneValidator.ValidateAsync(
            command.RequesterDiscordId, command.GuildId, command.GuildBranchId, command.GroupCount, command.SlotsPerGroup, command.RaidZoneIds, cancellationToken);
        if (validation.IsFailed)
            return Result<CommandResponse>.Fail(validation.Error!, validation.Detail);

        var distinctZoneIds = validation.Value!;

        var branch = await guildBranchesRepository.GetByIdAsync(command.GuildBranchId, cancellationToken);

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
            SignupMode = branch?.SignupMode ?? SignupMode.DefaultPresent,
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
