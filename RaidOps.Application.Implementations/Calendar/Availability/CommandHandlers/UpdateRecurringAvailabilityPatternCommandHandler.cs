using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRecurringAvailabilityPatternCommand"/> by verifying roster access and
/// inserting a new pattern version effective from today, non-retroactively. The previous version is
/// closed as of yesterday if it has already applied to at least one past date (so history for those
/// dates stays intact); otherwise — created and edited again on the same day, with nothing to
/// protect — it's deleted outright to avoid a zero-duration version cluttering the table.
/// </summary>
public class UpdateRecurringAvailabilityPatternCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateRecurringAvailabilityPatternCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRecurringAvailabilityPatternCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var validationFailure = RecurringAvailabilityPatternRequestHelper.ValidateCycleAndDays(command.CycleLengthDays, command.Days);
        if (validationFailure != null)
            return validationFailure;

        var existing = await availabilityRepository.GetPatternByIdAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RecurringAvailabilityPatternNotFound, $"Pattern '{command.PatternId}' does not exist.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (existing.EffectiveFrom < today)
            await availabilityRepository.ClosePatternAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, today.AddDays(-1), cancellationToken);
        else
            await availabilityRepository.DeletePatternAsync(command.PatternId, command.RequesterDiscordId, command.GuildId, cancellationToken);

        var pattern = await availabilityRepository.AddPatternAsync(new RecurringAvailabilityPattern
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = command.GuildId,
            Label = command.Label,
            CycleLengthDays = command.CycleLengthDays,
            AnchorDate = command.AnchorDate,
            EffectiveFrom = today,
            EffectiveUntil = null,
            Days = RecurringAvailabilityPatternRequestHelper.MapDays(command.Days),
        }, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RecurringAvailabilityPatternUpdated,
            RecurringAvailabilityPatternRequestHelper.BuildAuditVariables(command.Label, command.CycleLengthDays, command.AnchorDate, command.Days),
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Recurring availability pattern updated successfully.", new { pattern.Id }));
    }
}
