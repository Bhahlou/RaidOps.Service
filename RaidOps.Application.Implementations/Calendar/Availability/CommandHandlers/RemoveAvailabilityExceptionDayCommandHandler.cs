using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="RemoveAvailabilityExceptionDayCommand"/> by clearing a single day out of an
/// existing exception — shrinking it from an edge, splitting it into two rows if the day falls in
/// the middle, or deleting it outright if it was the only day covered — then announcing only the
/// net change via <see cref="IAvailabilityChangeAnnouncer"/> (a single "1 day removed" instead of
/// the raw "N days deleted" + up to two "M days added" a naive delete + re-create would produce).
/// Ownership (id + <see cref="RemoveAvailabilityExceptionDayCommand.RequesterDiscordId"/>) is the
/// only authorization needed; no separate guild access check applies.
/// </summary>
public class RemoveAvailabilityExceptionDayCommandHandler(
    IAvailabilityRepository availabilityRepository,
    IAvailabilityChangeAnnouncer availabilityChangeAnnouncer) : ICommandHandlerAsync<RemoveAvailabilityExceptionDayCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(RemoveAvailabilityExceptionDayCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await availabilityRepository.GetExceptionByIdAsync(command.ExceptionId, command.RequesterDiscordId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.AvailabilityExceptionNotFound, $"Exception '{command.ExceptionId}' does not exist.");

        if (existing.EndDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked, "Cannot edit a declaration that has already fully elapsed.");

        if (command.Date < existing.StartDate || command.Date > existing.EndDate)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "Date must fall within the exception's current range.");

        var windowStart = existing.StartDate;
        var windowEnd = existing.EndDate;

        var beforeExceptions = await availabilityRepository.GetExceptionsOverlappingAsync(
            command.RequesterDiscordId, windowStart, windowEnd, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsAsync(command.RequesterDiscordId, cancellationToken);

        await availabilityRepository.DeleteExceptionAsync(command.ExceptionId, command.RequesterDiscordId, cancellationToken);

        var remainingFragments = new List<AvailabilityDeclaration>();
        if (command.Date > existing.StartDate)
        {
            remainingFragments.Add(await availabilityRepository.AddExceptionAsync(
                CloneFragment(existing, existing.StartDate, command.Date.AddDays(-1)), cancellationToken));
        }

        if (command.Date < existing.EndDate)
        {
            remainingFragments.Add(await availabilityRepository.AddExceptionAsync(
                CloneFragment(existing, command.Date.AddDays(1), existing.EndDate), cancellationToken));
        }

        var afterExceptions = beforeExceptions.Where(e => e.Id != existing.Id).Concat(remainingFragments).ToList();

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

        return Result<CommandResponse>.Ok(new CommandResponse("Availability exception day removed successfully."));
    }

    private static AvailabilityDeclaration CloneFragment(AvailabilityDeclaration source, DateOnly startDate, DateOnly endDate) => new()
    {
        UserDiscordId = source.UserDiscordId,
        GuildId = source.GuildId,
        GuildBranchId = source.GuildBranchId,
        StartDate = startDate,
        EndDate = endDate,
        Status = source.Status,
        Reason = source.Reason,
        AvailableFrom = source.AvailableFrom,
        AvailableUntil = source.AvailableUntil,
    };
}
