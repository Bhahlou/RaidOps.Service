using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;

/// <summary>
/// Handles <see cref="UnassignSlotCommand"/> by clearing a (group, slot) coordinate. Composition
/// changes on an already-published event are audit-logged and posted to Discord (the "raid
/// composition changes" family); the same edit on a still-draft event stays silent, like the rest
/// of series/event lifecycle actions do for drafts.
/// </summary>
public class UnassignSlotCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IRaidCompositionRepository raidCompositionRepository,
    IRaidCompositionNotifier raidCompositionNotifier) : ICommandHandlerAsync<UnassignSlotCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UnassignSlotCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        var occupant = raidEvent.Assignments.FirstOrDefault(a => a.GroupNumber == command.GroupNumber && a.SlotNumber == command.SlotNumber);

        var unassigned = await raidCompositionRepository.UnassignAsync(command.EventId, command.GroupNumber, command.SlotNumber, cancellationToken);
        if (!unassigned)
            return Result<CommandResponse>.Fail(ResponseDetail.NotFound, "This slot was already empty.");

        if (raidEvent.PublicationStatus == RaidPublicationStatus.Published && occupant != null)
        {
            var character = await characterRepository.GetByIdAsync(occupant.CharacterId, cancellationToken);
            var characterName = character?.Name ?? occupant.CharacterId.ToString();
            var raidSpecs = await characterRepository.GetRaidSpecsAsync(occupant.CharacterId, cancellationToken);
            var specName = raidSpecs.FirstOrDefault(s => s.SpecId == occupant.SpecId)?.Spec.Name;

            await raidCompositionNotifier.NotifySlotUnassignedAsync(
                raidEvent, command.RequesterDiscordId,
                new RaidCharacterRef(characterName, character?.ClassId, specName),
                occupant.AssignedPlayerDiscordId,
                new SlotCoordinate(command.GroupNumber, command.SlotNumber),
                cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Slot cleared successfully."));
    }
}
