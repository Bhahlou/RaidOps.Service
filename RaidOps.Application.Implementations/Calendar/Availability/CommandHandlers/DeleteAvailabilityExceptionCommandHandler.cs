using RaidOps.Application.Contracts.Calendar.Availability.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.CommandHandlers;

/// <summary>
/// Handles <see cref="DeleteAvailabilityExceptionCommand"/> by verifying roster access and
/// deleting the exception, scoped to the requester's own declarations. An exception that has fully
/// elapsed (its last covered day is before today) is locked — it's the historical record of what
/// was actually declared at the time, and can no longer be erased after the fact.
/// </summary>
public class DeleteAvailabilityExceptionCommandHandler(
    IGuildAccessService guildAccessService,
    IAvailabilityRepository availabilityRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeleteAvailabilityExceptionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteAvailabilityExceptionCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var existing = await availabilityRepository.GetExceptionByIdAsync(command.ExceptionId, command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.AvailabilityExceptionNotFound, $"Exception '{command.ExceptionId}' does not exist.");

        if (existing.EndDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<CommandResponse>.Fail(ResponseDetail.PastDeclarationLocked, "Cannot delete a declaration that has already fully elapsed.");

        var deleted = await availabilityRepository.DeleteExceptionAsync(command.ExceptionId, command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (!deleted)
            return Result<CommandResponse>.Fail(ResponseDetail.AvailabilityExceptionNotFound, $"Exception '{command.ExceptionId}' does not exist.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.AvailabilityExceptionDeleted,
            new Dictionary<string, string>
            {
                ["startDate"] = existing.StartDate.ToString("yyyy-MM-dd"),
                ["endDate"] = existing.EndDate.ToString("yyyy-MM-dd"),
                ["status"] = existing.Status.ToString(),
                ["availableFrom"] = existing.AvailableFrom?.ToString("HH:mm:ss") ?? string.Empty,
                ["availableUntil"] = existing.AvailableUntil?.ToString("HH:mm:ss") ?? string.Empty,
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Availability exception deleted successfully."));
    }
}
