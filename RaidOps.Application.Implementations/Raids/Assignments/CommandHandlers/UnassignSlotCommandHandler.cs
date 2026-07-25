using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;

/// <summary>
/// Handles <see cref="UnassignSlotCommand"/> by clearing a (group, slot) coordinate. No audit
/// log entry is written — individual drag/drop operations are intentionally not audited, unlike
/// series/event lifecycle actions.
/// </summary>
public class UnassignSlotCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionRepository raidCompositionRepository) : ICommandHandlerAsync<UnassignSlotCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UnassignSlotCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var unassigned = await raidCompositionRepository.UnassignAsync(command.EventId, command.GroupNumber, command.SlotNumber, cancellationToken);
        if (!unassigned)
            return Result<CommandResponse>.Fail(ResponseDetail.NotFound, "This slot was already empty.");

        return Result<CommandResponse>.Ok(new CommandResponse("Slot cleared successfully."));
    }
}
