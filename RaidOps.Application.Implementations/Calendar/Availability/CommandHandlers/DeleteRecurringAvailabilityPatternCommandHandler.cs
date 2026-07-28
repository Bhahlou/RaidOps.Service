using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="DeleteRecurringAvailabilityPatternCommand"/> by stopping the pattern
/// non-retroactively: closed as of yesterday if it has already applied to at least one past date
/// (past resolved days stay exactly as they were), or deleted outright if it was created and
/// removed on the same day, with no history to protect. Ownership (id + <see cref="DeleteRecurringAvailabilityPatternCommand.RequesterDiscordId"/>)
/// is the only authorization needed; no separate guild access check applies. Audit log and Discord
/// notification only apply to a branch-scoped pattern — a Global one has no single guild to
/// log/notify against; properly announcing it means fanning out across every branch where the
/// member has an active roster character (not implemented yet, calendar global rework Phase C), so
/// it's silently unannounced until then.
/// </summary>
public class DeleteRecurringAvailabilityPatternCommandHandler(
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService,
    IGuildNotificationDispatcher guildNotificationDispatcher,
    IAbsenceNotificationContentBuilder absenceNotificationContentBuilder) : ICommandHandlerAsync<DeleteRecurringAvailabilityPatternCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteRecurringAvailabilityPatternCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await availabilityRepository.GetPatternByIdAsync(command.PatternId, command.RequesterDiscordId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound, $"Pattern '{command.PatternId}' does not exist.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stopped = existing.EffectiveFrom < today
            ? await availabilityRepository.ClosePatternAsync(command.PatternId, command.RequesterDiscordId, today.AddDays(-1), cancellationToken)
            : await availabilityRepository.DeletePatternAsync(command.PatternId, command.RequesterDiscordId, cancellationToken);

        if (!stopped)
            return Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound, $"Pattern '{command.PatternId}' does not exist.");

        if (existing.GuildId != null)
        {
            await auditLogService.LogAsync(
                existing.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.RecurringAvailabilityPatternStopped,
                RecurringAvailabilityPatternRequestHelper.BuildAuditVariables(existing.Label, existing.CycleLengthDays, existing.AnchorDate, existing.Days),
                cancellationToken);

            var eventType = GuildNotificationEventType.AbsenceRemoved;

            var embed = await absenceNotificationContentBuilder.BuildPatternAsync(
                existing.GuildId,
                command.RequesterDiscordId,
                eventType,
                existing.AnchorDate,
                existing.CycleLengthDays,
                [.. existing.Days.Select(d => new PatternDayNotification(d.OffsetInCycle, d.Status, d.Reason, d.AvailableFrom, d.AvailableUntil))],
                cancellationToken);

            await guildNotificationDispatcher.NotifyAsync(existing.GuildId, eventType, embed, cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern stopped successfully."));
    }
}
