using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;

/// <summary>Handles <see cref="ClearRaidEventAttributionCommand"/> by validating the officer's access, then clearing the filled slot.</summary>
public class ClearRaidEventAttributionCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventAttributionsRepository attributionsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<ClearRaidEventAttributionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(ClearRaidEventAttributionCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var cleared = await attributionsRepository.ClearAsync(command.EventId, command.CellId, command.InstanceIndex, cancellationToken);
        if (!cleared)
            return Result<CommandResponse>.Fail(ResponseDetail.SlotEmpty, "This attribution slot is already empty.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidEventAttributionUpdated,
            new Dictionary<string, string> { ["definitionId"] = command.DefinitionId.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Attribution slot cleared successfully."));
    }
}
