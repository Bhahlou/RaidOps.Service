using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;

/// <summary>
/// Handles <see cref="SwapSlotAssignmentsCommand"/> by exchanging the two coordinates' assigned
/// characters in a single atomic operation. No re-validation of roster eligibility, declared
/// absence, or lockout conflicts — both characters already hold a legitimate assignment in this
/// same event, and swapping their (group, slot) coordinate changes none of the facts those checks
/// depend on (event, date, target zones). Composition changes on an already-published event are
/// audit-logged and posted to Discord (the "raid composition changes" family); the same swap on a
/// still-draft event stays silent, matching <see cref="UnassignSlotCommandHandler"/>.
/// </summary>
public class SwapSlotAssignmentsCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IRaidCompositionRepository raidCompositionRepository,
    IRaidCompositionNotifier raidCompositionNotifier) : ICommandHandlerAsync<SwapSlotAssignmentsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(SwapSlotAssignmentsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        if (!IsInBounds(command.GroupNumberA, command.SlotNumberA, raidEvent.GroupCount, raidEvent.SlotsPerGroup) ||
            !IsInBounds(command.GroupNumberB, command.SlotNumberB, raidEvent.GroupCount, raidEvent.SlotsPerGroup))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidGroupOrSlotNumber, "Group/slot number is out of the event's grid bounds.");

        // Dropped back onto its own slot — a harmless no-op, not an error.
        if (command.GroupNumberA == command.GroupNumberB && command.SlotNumberA == command.SlotNumberB)
            return Result<CommandResponse>.Ok(new CommandResponse("Nothing to swap."));

        var occupantA = raidEvent.Assignments.FirstOrDefault(a => a.GroupNumber == command.GroupNumberA && a.SlotNumber == command.SlotNumberA);
        var occupantB = raidEvent.Assignments.FirstOrDefault(a => a.GroupNumber == command.GroupNumberB && a.SlotNumber == command.SlotNumberB);

        var swapped = await raidCompositionRepository.SwapAssignmentsAsync(
            command.EventId, command.GroupNumberA, command.SlotNumberA, command.GroupNumberB, command.SlotNumberB, cancellationToken);

        if (!swapped)
            return Result<CommandResponse>.Fail(ResponseDetail.BothSlotsMustBeOccupiedToSwap, "Both slots must be occupied to swap them.");

        if (raidEvent.PublicationStatus == RaidPublicationStatus.Published && occupantA != null && occupantB != null)
        {
            var characterA = await characterRepository.GetByIdAsync(occupantA.CharacterId, cancellationToken);
            var characterB = await characterRepository.GetByIdAsync(occupantB.CharacterId, cancellationToken);
            var characterAName = characterA?.Name ?? occupantA.CharacterId.ToString();
            var characterBName = characterB?.Name ?? occupantB.CharacterId.ToString();
            var raidSpecsA = await characterRepository.GetRaidSpecsAsync(occupantA.CharacterId, cancellationToken);
            var raidSpecsB = await characterRepository.GetRaidSpecsAsync(occupantB.CharacterId, cancellationToken);
            var specAName = raidSpecsA.FirstOrDefault(s => s.SpecId == occupantA.SpecId)?.Spec.Name;
            var specBName = raidSpecsB.FirstOrDefault(s => s.SpecId == occupantB.SpecId)?.Spec.Name;

            await raidCompositionNotifier.NotifySlotsSwappedAsync(
                raidEvent, command.RequesterDiscordId,
                new RaidCharacterRef(characterAName, characterA?.ClassId, specAName), new SlotCoordinate(command.GroupNumberA, command.SlotNumberA),
                new RaidCharacterRef(characterBName, characterB?.ClassId, specBName), new SlotCoordinate(command.GroupNumberB, command.SlotNumberB),
                cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Characters swapped successfully."));
    }

    private static bool IsInBounds(int groupNumber, int slotNumber, int groupCount, int slotsPerGroup) =>
        groupNumber >= 1 && groupNumber <= groupCount && slotNumber >= 1 && slotNumber <= slotsPerGroup;
}
