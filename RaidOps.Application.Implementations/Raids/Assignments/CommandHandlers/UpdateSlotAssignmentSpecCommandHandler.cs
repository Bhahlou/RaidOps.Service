using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateSlotAssignmentSpecCommand"/> — validates the requester is an officer,
/// the target coordinate holds an assignment, and the chosen spec is one the character actually
/// declared as raid-viable, then persists the change.
/// </summary>
public class UpdateSlotAssignmentSpecCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IRaidCompositionRepository raidCompositionRepository) : ICommandHandlerAsync<UpdateSlotAssignmentSpecCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateSlotAssignmentSpecCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        var assignment = raidEvent.Assignments.FirstOrDefault(a => a.GroupNumber == command.GroupNumber && a.SlotNumber == command.SlotNumber);
        if (assignment == null)
            return Result<CommandResponse>.Fail(ResponseDetail.SlotEmpty, "This slot has no assignment to change the spec of.");

        var raidSpecs = await characterRepository.GetRaidSpecsAsync(assignment.CharacterId, cancellationToken);
        if (!raidSpecs.Any(s => s.SpecId == command.SpecId))
            return Result<CommandResponse>.Fail(ResponseDetail.SpecNotAvailableForCharacter, "This spec is not one of the character's declared raid specs.");

        await raidCompositionRepository.UpdateAssignmentSpecAsync(command.EventId, command.GroupNumber, command.SlotNumber, command.SpecId, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Assignment spec updated successfully."));
    }
}
