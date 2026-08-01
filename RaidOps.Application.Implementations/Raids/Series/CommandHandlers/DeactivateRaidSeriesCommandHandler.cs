using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Series.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Series.CommandHandlers;

/// <summary>
/// Handles <see cref="DeactivateRaidSeriesCommand"/> by verifying officer access and stopping
/// future materialization. Occurrences already produced by the series are left untouched, unless
/// <see cref="DeactivateRaidSeriesCommand.DeleteEmptyOccurrences"/> asks to also bulk delete the
/// ones still empty and unpublished (see <see cref="IRaidEventRepository.DeleteEmptyDraftOccurrencesForSeriesAsync"/>).
/// </summary>
public class DeactivateRaidSeriesCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidSeriesRepository raidSeriesRepository,
    IRaidEventRepository raidEventRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeactivateRaidSeriesCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeactivateRaidSeriesCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var deactivated = await raidSeriesRepository.DeactivateAsync(command.SeriesId, command.GuildBranchId, cancellationToken);
        if (!deactivated)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidSeriesNotFound, $"Raid series '{command.SeriesId}' does not exist.");

        var deletedCount = 0;
        if (command.DeleteEmptyOccurrences)
            deletedCount = await raidEventRepository.DeleteEmptyDraftOccurrencesForSeriesAsync(command.SeriesId, command.GuildBranchId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidSeriesDeactivated,
            new Dictionary<string, string> { ["seriesId"] = command.SeriesId.ToString(), ["deletedEmptyOccurrences"] = deletedCount.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid series deactivated successfully.", new { deletedCount }));
    }
}
