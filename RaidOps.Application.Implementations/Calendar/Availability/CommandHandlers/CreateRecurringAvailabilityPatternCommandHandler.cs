using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="CreateRecurringAvailabilityPatternCommand"/> by verifying roster access (for
/// a branch-scoped pattern — a Global one just needs an authenticated member) and persisting the
/// pattern along with its day set. A branch-scoped pattern audit-logs/notifies that one branch; a
/// Global pattern has no single guild to log/notify against, so it fans out identically to every
/// branch where the member currently has an active roster character.
/// </summary>
public class CreateRecurringAvailabilityPatternCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IActiveRosterBranchResolver activeRosterBranchResolver,
    IAuditLogService auditLogService,
    IGuildNotificationDispatcher guildNotificationDispatcher,
    IAbsenceNotificationContentBuilder absenceNotificationContentBuilder) : ICommandHandlerAsync<CreateRecurringAvailabilityPatternCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRecurringAvailabilityPatternCommand command, CancellationToken cancellationToken = default)
    {
        if ((command.GuildId == null) != (command.GuildBranchId == null))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "GuildId and GuildBranchId must be both set (a specific branch) or both null (Global).");

        if (command.GuildId != null)
        {
            var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId!.Value, cancellationToken);
            if (accessLevel < GuildAccessLevel.Roster)
                return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this branch's roster.");
        }

        var validationFailure = RecurringAvailabilityPatternRequestHelper.ValidateCycleAndDays(command.CycleLengthDays, command.Days);
        if (validationFailure != null)
            return validationFailure;

        var pattern = await availabilityRepository.AddPatternAsync(new RecurringAvailabilityPattern
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = command.GuildId,
            GuildBranchId = command.GuildBranchId,
            Label = command.Label,
            CycleLengthDays = command.CycleLengthDays,
            AnchorDate = command.AnchorDate,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            EffectiveUntil = null,
            Days = RecurringAvailabilityPatternRequestHelper.MapDays(command.Days),
        }, cancellationToken);

        var auditVariables = RecurringAvailabilityPatternRequestHelper.BuildAuditVariables(command.Label, command.CycleLengthDays, command.AnchorDate, command.Days);
        List<PatternDayNotification> days = [.. command.Days.Select(d => new PatternDayNotification(d.OffsetInCycle, d.Status, d.Reason, d.AvailableFrom, d.AvailableUntil))];
        var announcement = new PatternAnnouncement(command.AnchorDate, command.CycleLengthDays, days, auditVariables);

        if (command.GuildId != null)
        {
            await AnnouncePatternAsync(new ActiveRosterBranch(command.GuildId, command.GuildBranchId!.Value), command.RequesterDiscordId, announcement, cancellationToken);
        }
        else
        {
            var activeBranches = await activeRosterBranchResolver.GetActiveBranchesAsync(command.RequesterDiscordId, cancellationToken);
            foreach (var branch in activeBranches)
                await AnnouncePatternAsync(branch, command.RequesterDiscordId, announcement, cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern created successfully.", new { pattern.Id }));
    }

    private async Task AnnouncePatternAsync(
        ActiveRosterBranch branch,
        string requesterDiscordId,
        PatternAnnouncement announcement,
        CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(branch.GuildId, requesterDiscordId, GuildAuditAction.RecurringAvailabilityPatternCreated, announcement.AuditVariables, cancellationToken);

        var eventType = GuildNotificationEventType.AbsenceAdded;

        var embed = await absenceNotificationContentBuilder.BuildPatternAsync(
            branch.GuildId, requesterDiscordId, eventType, announcement.AnchorDate, announcement.CycleLengthDays, announcement.Days, cancellationToken);

        await guildNotificationDispatcher.NotifyAsync(branch.GuildId, eventType, branch.GuildBranchId, embed, cancellationToken);
    }
}
