using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="CreateAvailabilityExceptionCommand"/> by verifying roster access and
/// persisting the one-off exception. Refuses to start before today — declarations must be made
/// ahead of (or on) the day they apply to, so a member can't retroactively invent an excuse for a
/// day that's already passed.
/// </summary>
public class CreateAvailabilityExceptionCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateAvailabilityExceptionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateAvailabilityExceptionCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        if (command.EndDate < command.StartDate)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, "EndDate must be on or after StartDate.");

        if (command.StartDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked, "Cannot declare an exception starting in the past.");

        var exception = await availabilityRepository.AddExceptionAsync(new AvailabilityDeclaration
        {
            UserDiscordId = command.RequesterDiscordId,
            GuildId = command.GuildId,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Status = command.Status,
            Reason = command.Reason,
            AvailableFrom = command.AvailableFrom,
            AvailableUntil = command.AvailableUntil,
        }, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.AvailabilityExceptionDeclared,
            new Dictionary<string, string>
            {
                ["startDate"] = command.StartDate.ToString("yyyy-MM-dd"),
                ["endDate"] = command.EndDate.ToString("yyyy-MM-dd"),
                ["status"] = command.Status.ToString(),
                ["availableFrom"] = command.AvailableFrom?.ToString("HH:mm:ss") ?? string.Empty,
                ["availableUntil"] = command.AvailableUntil?.ToString("HH:mm:ss") ?? string.Empty,
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Availability exception created successfully.", new { exception.Id }));
    }
}
