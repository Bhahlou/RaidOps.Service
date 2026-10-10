using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRecurringAvailabilityPatternCommand"/> by inserting a new pattern
/// version effective from today, non-retroactively. The previous version is closed as of yesterday
/// if it has already applied to at least one past date (so history for those dates stays intact);
/// otherwise — created and edited again on the same day, with nothing to protect — it's deleted
/// outright to avoid a zero-duration version cluttering the table. Scope (Global/branch) is
/// immutable after creation — only Create chooses it — so the new version is re-created with the
/// existing pattern's scope, not a resubmitted one. Ownership (id + <see cref="UpdateRecurringAvailabilityPatternCommand.RequesterDiscordId"/>)
/// is the only authorization needed; no separate guild access check applies. Audit log only applies
/// to a branch-scoped pattern — a Global one has no single guild to log against; properly announcing
/// it means fanning out across every branch where the member has an active roster character (not
/// implemented yet, calendar global rework Phase C), so it's silently unlogged until then.
/// </summary>
public class UpdateRecurringAvailabilityPatternCommandHandler(
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateRecurringAvailabilityPatternCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRecurringAvailabilityPatternCommand command, CancellationToken cancellationToken = default)
    {
        var validationFailure = RecurringAvailabilityPatternRequestHelper.ValidateCycleAndDays(command.CycleLengthDays, command.Days);
        if (validationFailure != null)
            return validationFailure;

        var existing = await availabilityRepository.GetPatternByIdAsync(command.PatternId, command.RequesterDiscordId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound, $"Pattern '{command.PatternId}' does not exist.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (existing.EffectiveFrom < today)
            await availabilityRepository.ClosePatternAsync(command.PatternId, command.RequesterDiscordId, today.AddDays(-1), cancellationToken);
        else
            await availabilityRepository.DeletePatternAsync(command.PatternId, command.RequesterDiscordId, cancellationToken);

        var pattern = await availabilityRepository.AddPatternAsync(new RecurringAvailabilityPattern
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = existing.GuildId,
            GuildBranchId = existing.GuildBranchId,
            Label = command.Label,
            CycleLengthDays = command.CycleLengthDays,
            AnchorDate = command.AnchorDate,
            EffectiveFrom = today,
            EffectiveUntil = null,
            Days = RecurringAvailabilityPatternRequestHelper.MapDays(command.Days),
        }, cancellationToken);

        if (existing.GuildId != null)
        {
            await auditLogService.LogAsync(
                existing.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.RecurringAvailabilityPatternUpdated,
                RecurringAvailabilityPatternRequestHelper.BuildAuditVariables(command.Label, command.CycleLengthDays, command.AnchorDate, command.Days),
                cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern updated successfully.", new { pattern.Id }));
    }
}
