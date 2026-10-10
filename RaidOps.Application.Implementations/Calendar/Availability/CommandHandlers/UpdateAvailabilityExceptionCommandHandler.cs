using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateAvailabilityExceptionCommand"/> by replacing the exception in place
/// (internally a delete + re-create — the same non-atomic pattern already used for recurring
/// pattern edits) and announcing only the net change via <see cref="IAvailabilityChangeAnnouncer"/>,
/// instead of the raw delete/create pair a caller doing this itself would produce. Scope
/// (Global/branch) is immutable after creation — only Create chooses it — so the new row is
/// re-created with <paramref name="command"/>'s existing scope, not a resubmitted one. Ownership
/// (id + <see cref="UpdateAvailabilityExceptionCommand.RequesterDiscordId"/>) is the only
/// authorization needed; no separate guild access check applies.
/// </summary>
public class UpdateAvailabilityExceptionCommandHandler(
    IAvailabilityRepository availabilityRepository,
    IAvailabilityChangeAnnouncer availabilityChangeAnnouncer) : ICommandHandlerAsync<UpdateAvailabilityExceptionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateAvailabilityExceptionCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await availabilityRepository.GetExceptionByIdAsync(command.ExceptionId, command.RequesterDiscordId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.AvailabilityExceptionNotFound, $"Exception '{command.ExceptionId}' does not exist.");

        if (existing.EndDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked, "Cannot edit a declaration that has already fully elapsed.");

        if (command.EndDate < command.StartDate)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "EndDate must be on or after StartDate.");

        if (command.StartDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked, "Cannot declare an exception starting in the past.");

        if (command.Status == DayAvailabilityStatus.Partial && command.AvailableFrom == null && command.AvailableUntil == null)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "A Partial declaration needs at least one of AvailableFrom/AvailableUntil.");

        var windowStart = existing.StartDate < command.StartDate ? existing.StartDate : command.StartDate;
        var windowEnd = existing.EndDate > command.EndDate ? existing.EndDate : command.EndDate;

        var beforeExceptions = await availabilityRepository.GetExceptionsOverlappingAsync(
            command.RequesterDiscordId, windowStart, windowEnd, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsAsync(command.RequesterDiscordId, cancellationToken);

        await availabilityRepository.DeleteExceptionAsync(command.ExceptionId, command.RequesterDiscordId, cancellationToken);
        var updated = await availabilityRepository.AddExceptionAsync(new AvailabilityDeclaration
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = existing.GuildId,
            GuildBranchId = existing.GuildBranchId,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Status = command.Status,
            Reason = command.Reason,
            AvailableFrom = command.AvailableFrom,
            AvailableUntil = command.AvailableUntil,
        }, cancellationToken);

        var afterExceptions = beforeExceptions.Where(e => e.Id != existing.Id).Append(updated).ToList();

        await availabilityChangeAnnouncer.AnnounceAsync(
            new AvailabilityChange(
                existing.GuildId,
                existing.GuildBranchId,
                command.RequesterDiscordId,
                windowStart,
                windowEnd,
                beforeExceptions,
                afterExceptions,
                patterns),
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Availability exception updated successfully.", new { updated.Id }));
    }
}
