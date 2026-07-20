using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="DeleteRecurringAvailabilityPatternCommand"/> by verifying roster access and
/// stopping the pattern non-retroactively: closed as of yesterday if it has already applied to at
/// least one past date (past resolved days stay exactly as they were), or deleted outright if it was
/// created and removed on the same day, with no history to protect.
/// </summary>
public class DeleteRecurringAvailabilityPatternCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeleteRecurringAvailabilityPatternCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteRecurringAvailabilityPatternCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var existing = await availabilityRepository.GetPatternByIdAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound, $"Pattern '{command.PatternId}' does not exist.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stopped = existing.EffectiveFrom < today
            ? await availabilityRepository.ClosePatternAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, today.AddDays(-1), cancellationToken)
            : await availabilityRepository.DeletePatternAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, cancellationToken);

        if (!stopped)
            return Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound, $"Pattern '{command.PatternId}' does not exist.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RecurringAvailabilityPatternStopped,
            RecurringAvailabilityPatternRequestHelper.BuildAuditVariables(existing.Label, existing.CycleLengthDays, existing.AnchorDate, existing.Days),
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern stopped successfully."));
    }
}
