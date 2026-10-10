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
/// declared as raid-viable, then persists the change. Composition changes on an already-published
/// event are audit-logged and posted to Discord (the "raid composition changes" family); the same
/// edit on a still-draft event stays silent.
/// </summary>
public class UpdateSlotAssignmentSpecCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IRaidCompositionRepository raidCompositionRepository,
    IRaidCompositionNotifier raidCompositionNotifier) : ICommandHandlerAsync<UpdateSlotAssignmentSpecCommand>
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
        var newSpec = raidSpecs.FirstOrDefault(s => s.SpecId == command.SpecId);
        if (newSpec == null)
            return Result<CommandResponse>.Fail(ResponseDetail.SpecNotAvailableForCharacter, "This spec is not one of the character's declared raid specs.");

        // Captured from the not-yet-updated `assignment` before UpdateAssignmentSpecAsync mutates
        // the DB row — the character may no longer declare its old spec as raid-viable, so fall back
        // to the raw spec ID rather than failing the audit trail over a stale declaration.
        var oldSpecName = raidSpecs.FirstOrDefault(s => s.SpecId == assignment.SpecId)?.Spec.Name ?? assignment.SpecId.ToString();

        await raidCompositionRepository.UpdateAssignmentSpecAsync(command.EventId, command.GroupNumber, command.SlotNumber, command.SpecId, cancellationToken);

        if (raidEvent.PublicationStatus == RaidPublicationStatus.Published)
        {
            var character = await characterRepository.GetByIdAsync(assignment.CharacterId, cancellationToken);
            var characterName = character?.Name ?? assignment.CharacterId.ToString();

            await raidCompositionNotifier.NotifySlotSpecChangedAsync(
                raidEvent, command.RequesterDiscordId,
                new RaidCharacterRef(characterName, character?.ClassId),
                assignment.AssignedPlayerDiscordId,
                oldSpecName, newSpec.Spec.Name,
                cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Assignment spec updated successfully."));
    }
}
