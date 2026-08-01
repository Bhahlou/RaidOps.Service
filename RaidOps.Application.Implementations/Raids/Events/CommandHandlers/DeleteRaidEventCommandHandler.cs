using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="DeleteRaidEventCommand"/> by verifying officer access and permanently
/// removing a raid event — its slot assignments are cascade-deleted with it at the database level.
/// </summary>
public class DeleteRaidEventCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeleteRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var existing = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        await raidEventRepository.DeleteAsync(command.EventId, command.GuildBranchId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventDeleted,
            new Dictionary<string, string> { ["eventName"] = existing.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event deleted successfully."));
    }
}
