using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="DeleteAvailabilityExceptionCommand"/> by deleting the exception, scoped to
/// the requester's own declarations (ownership by id + <see cref="DeleteAvailabilityExceptionCommand.RequesterDiscordId"/>
/// is the only authorization needed; no separate guild access check applies). An exception that has
/// fully elapsed (its last covered day is before today) is locked — it's the historical record of
/// what was actually declared at the time, and can no longer be erased after the fact.
/// </summary>
public class DeleteAvailabilityExceptionCommandHandler(
    IAvailabilityRepository availabilityRepository,
    IAvailabilityChangeAnnouncer availabilityChangeAnnouncer) : ICommandHandlerAsync<DeleteAvailabilityExceptionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteAvailabilityExceptionCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await availabilityRepository.GetExceptionByIdAsync(command.ExceptionId, command.RequesterDiscordId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.AvailabilityExceptionNotFound, $"Exception '{command.ExceptionId}' does not exist.");

        if (existing.EndDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked, "Cannot delete a declaration that has already fully elapsed.");

        var beforeExceptions = await availabilityRepository.GetExceptionsOverlappingAsync(
            command.RequesterDiscordId, existing.StartDate, existing.EndDate, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsAsync(command.RequesterDiscordId, cancellationToken);

        var deleted = await availabilityRepository.DeleteExceptionAsync(command.ExceptionId, command.RequesterDiscordId, cancellationToken);
        if (!deleted)
            return Result<CommandResponse>.Fail(ResponseDetail.AvailabilityExceptionNotFound, $"Exception '{command.ExceptionId}' does not exist.");

        var afterExceptions = beforeExceptions.Where(e => e.Id != existing.Id).ToList();

        await availabilityChangeAnnouncer.AnnounceAsync(
            new AvailabilityChange(
                existing.GuildId,
                existing.GuildBranchId,
                command.RequesterDiscordId,
                existing.StartDate,
                existing.EndDate,
                beforeExceptions,
                afterExceptions,
                patterns),
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Availability exception deleted successfully."));
    }
}
