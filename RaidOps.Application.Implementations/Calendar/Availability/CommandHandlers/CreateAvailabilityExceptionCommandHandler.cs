using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="CreateAvailabilityExceptionCommand"/> by verifying roster access (for a
/// branch-scoped declaration — a Global one just needs an authenticated member) and persisting the
/// one-off exception. Refuses to start before today — declarations must be made ahead of (or on)
/// the day they apply to, so a member can't retroactively invent an excuse for a day that's already
/// passed. Notification/audit is delegated to <see cref="IAvailabilityChangeAnnouncer"/>, which
/// diffs resolved availability before/after instead of assuming "created" always means "absence
/// added" — declaring <c>Available</c> to override an otherwise-restrictive recurring pattern
/// naturally comes out as a removal, and declaring <c>Available</c> where nothing was restricted to
/// begin with produces no notification at all, since nothing actually changed.
/// </summary>
public class CreateAvailabilityExceptionCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAvailabilityChangeAnnouncer availabilityChangeAnnouncer) : ICommandHandlerAsync<CreateAvailabilityExceptionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateAvailabilityExceptionCommand command, CancellationToken cancellationToken = default)
    {
        if ((command.GuildId == null) != (command.GuildBranchId == null))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "GuildId and GuildBranchId must be both set (a specific branch) or both null (Global).");

        if (command.GuildId != null)
        {
            var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId!.Value, cancellationToken);
            if (accessLevel < GuildAccessLevel.Roster)
                return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this branch's roster.");
        }

        if (command.EndDate < command.StartDate)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "EndDate must be on or after StartDate.");

        if (command.StartDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked, "Cannot declare an exception starting in the past.");

        if (command.Status == DayAvailabilityStatus.Partial && command.AvailableFrom == null && command.AvailableUntil == null)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "A Partial declaration needs at least one of AvailableFrom/AvailableUntil.");

        var beforeExceptions = await availabilityRepository.GetExceptionsOverlappingAsync(
            command.RequesterDiscordId, command.StartDate, command.EndDate, cancellationToken);
        var patterns = await availabilityRepository.GetPatternsAsync(command.RequesterDiscordId, cancellationToken);

        var exception = await availabilityRepository.AddExceptionAsync(new AvailabilityDeclaration
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = command.GuildId,
            GuildBranchId = command.GuildBranchId,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Status = command.Status,
            Reason = command.Reason,
            AvailableFrom = command.AvailableFrom,
            AvailableUntil = command.AvailableUntil,
        }, cancellationToken);

        var afterExceptions = beforeExceptions.Append(exception).ToList();

        await availabilityChangeAnnouncer.AnnounceAsync(
            new AvailabilityChange(
                command.GuildId,
                command.GuildBranchId,
                command.RequesterDiscordId,
                command.StartDate,
                command.EndDate,
                beforeExceptions,
                afterExceptions,
                patterns),
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Availability exception created successfully.", new { exception.Id }));
    }
}
