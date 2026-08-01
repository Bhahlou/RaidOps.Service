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
/// depend on (event, date, target zones). No audit log entry, matching
/// <see cref="UnassignSlotCommandHandler"/> — individual drag/drop operations aren't audited.
/// </summary>
public class SwapSlotAssignmentsCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidCompositionRepository raidCompositionRepository) : ICommandHandlerAsync<SwapSlotAssignmentsCommand>
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

        var swapped = await raidCompositionRepository.SwapAssignmentsAsync(
            command.EventId, command.GroupNumberA, command.SlotNumberA, command.GroupNumberB, command.SlotNumberB, cancellationToken);

        if (!swapped)
            return Result<CommandResponse>.Fail(ResponseDetail.BothSlotsMustBeOccupiedToSwap, "Both slots must be occupied to swap them.");

        return Result<CommandResponse>.Ok(new CommandResponse("Characters swapped successfully."));
    }

    private static bool IsInBounds(int groupNumber, int slotNumber, int groupCount, int slotsPerGroup) =>
        groupNumber >= 1 && groupNumber <= groupCount && slotNumber >= 1 && slotNumber <= slotsPerGroup;
}
