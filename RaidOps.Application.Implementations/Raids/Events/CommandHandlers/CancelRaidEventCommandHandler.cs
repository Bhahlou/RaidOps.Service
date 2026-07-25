using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="CancelRaidEventCommand"/> by verifying officer access and marking the event
/// cancelled — it stops counting toward lockout consumption and the "unassigned members"
/// computation, but its assignments and history are preserved.
/// </summary>
public class CancelRaidEventCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CancelRaidEventCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CancelRaidEventCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var cancelled = await raidEventRepository.CancelAsync(command.EventId, command.GuildId, cancellationToken);
        if (!cancelled)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventCancelled,
            new Dictionary<string, string> { ["eventId"] = command.EventId.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid event cancelled successfully."));
    }
}
