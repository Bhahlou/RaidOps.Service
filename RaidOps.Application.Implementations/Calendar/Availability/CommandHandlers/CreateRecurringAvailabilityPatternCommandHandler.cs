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
/// pattern along with its day set. Audit log and Discord notification only apply to a branch-scoped
/// pattern — a Global one has no single guild to log/notify against; properly announcing it means
/// fanning out across every branch where the member has an active roster character (not implemented
/// yet, calendar global rework Phase C), so it's silently unannounced until then.
/// </summary>
public class CreateRecurringAvailabilityPatternCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
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

        if (command.GuildId != null)
        {
            await auditLogService.LogAsync(
                command.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.RecurringAvailabilityPatternCreated,
                RecurringAvailabilityPatternRequestHelper.BuildAuditVariables(command.Label, command.CycleLengthDays, command.AnchorDate, command.Days),
                cancellationToken);

            var eventType = GuildNotificationEventType.AbsenceAdded;

            var embed = await absenceNotificationContentBuilder.BuildPatternAsync(
                command.GuildId,
                command.RequesterDiscordId,
                eventType,
                command.AnchorDate,
                command.CycleLengthDays,
                [.. command.Days.Select(d => new PatternDayNotification(d.OffsetInCycle, d.Status, d.Reason, d.AvailableFrom, d.AvailableUntil))],
                cancellationToken);

            await guildNotificationDispatcher.NotifyAsync(command.GuildId, eventType, embed, cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern created successfully.", new { pattern.Id }));
    }
}
