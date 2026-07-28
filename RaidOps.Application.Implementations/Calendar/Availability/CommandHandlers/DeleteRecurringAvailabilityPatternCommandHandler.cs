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
/// is the only authorization needed; no separate guild access check applies. A branch-scoped
/// pattern audit-logs/notifies that one branch; a Global pattern has no single guild to log/notify
/// against, so it fans out identically to every branch where the member currently has an active
/// roster character.
/// </summary>
public class DeleteRecurringAvailabilityPatternCommandHandler(
    IAvailabilityRepository availabilityRepository,
    IActiveRosterBranchResolver activeRosterBranchResolver,
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

        var auditVariables = RecurringAvailabilityPatternRequestHelper.BuildAuditVariables(existing.Label, existing.CycleLengthDays, existing.AnchorDate, existing.Days);
        List<PatternDayNotification> days = [.. existing.Days.Select(d => new PatternDayNotification(d.OffsetInCycle, d.Status, d.Reason, d.AvailableFrom, d.AvailableUntil))];

        if (existing.GuildId != null)
        {
            await AnnouncePatternStoppedAsync(existing.GuildId, existing.GuildBranchId!.Value, command.RequesterDiscordId, auditVariables, existing.AnchorDate, existing.CycleLengthDays, days, cancellationToken);
        }
        else
        {
            var activeBranches = await activeRosterBranchResolver.GetActiveBranchesAsync(command.RequesterDiscordId, cancellationToken);
            foreach (var branch in activeBranches)
                await AnnouncePatternStoppedAsync(branch.GuildId, branch.GuildBranchId, command.RequesterDiscordId, auditVariables, existing.AnchorDate, existing.CycleLengthDays, days, cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern stopped successfully."));
    }

    private async Task AnnouncePatternStoppedAsync(
        string guildId,
        int guildBranchId,
        string requesterDiscordId,
        Dictionary<string, string> auditVariables,
        DateOnly anchorDate,
        int cycleLengthDays,
        IReadOnlyList<PatternDayNotification> days,
        CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(guildId, requesterDiscordId, GuildAuditAction.RecurringAvailabilityPatternStopped, auditVariables, cancellationToken);

        var eventType = GuildNotificationEventType.AbsenceRemoved;

        var embed = await absenceNotificationContentBuilder.BuildPatternAsync(
            guildId, requesterDiscordId, eventType, anchorDate, cycleLengthDays, days, cancellationToken);

        await guildNotificationDispatcher.NotifyAsync(guildId, eventType, guildBranchId, embed, cancellationToken);
    }
}
