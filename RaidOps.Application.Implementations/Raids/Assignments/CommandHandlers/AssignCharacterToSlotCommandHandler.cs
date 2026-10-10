using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;

/// <summary>
/// Handles <see cref="AssignCharacterToSlotCommand"/> — the central validation point of the raid
/// builder. Checks run in a fixed order (event state, roster eligibility on this specific guild
/// branch, grid bounds/occupancy, one-character-per-player, declared absence, lockout conflict) so
/// the first failing rule always produces the same, predictable error for a given bad request. A
/// drop onto an already-occupied slot is rejected outright — see
/// <see cref="SwapSlotAssignmentsCommandHandler"/> for exchanging two occupied slots. Composition
/// changes on an already-published event are audit-logged and posted to Discord (the "raid
/// composition changes" family); the same edits on a still-draft event stay silent.
/// </summary>
public class AssignCharacterToSlotCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IRaidSlotEligibilityValidator raidSlotEligibilityValidator,
    IRaidCompositionRepository raidCompositionRepository,
    IRaidCompositionNotifier raidCompositionNotifier) : ICommandHandlerAsync<AssignCharacterToSlotCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(AssignCharacterToSlotCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        var character = await characterRepository.GetByIdAsync(command.CharacterId, cancellationToken);
        if (character == null || !character.IsActiveInRaidOps)
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotOnRoster, "Character is not an active member of this guild branch's roster.");

        var membershipResult = await raidSlotEligibilityValidator.ValidateRosterMembershipAsync(command.CharacterId, command.GuildBranchId, cancellationToken);
        if (membershipResult.IsFailed)
            return Result<CommandResponse>.Fail(membershipResult.Error!, membershipResult.Detail);

        if (command.GroupNumber < 1 || command.GroupNumber > raidEvent.GroupCount || command.SlotNumber < 1 || command.SlotNumber > raidEvent.SlotsPerGroup)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidGroupOrSlotNumber, "Group/slot number is out of the event's grid bounds.");

        var slotOccupant = raidEvent.Assignments.FirstOrDefault(a => a.GroupNumber == command.GroupNumber && a.SlotNumber == command.SlotNumber);
        if (slotOccupant != null && slotOccupant.CharacterId != command.CharacterId)
            return Result<CommandResponse>.Fail(ResponseDetail.SlotOccupied, "This slot is already occupied — unassign it first.");

        var otherCharacterOfSamePlayer = raidEvent.Assignments.Any(a => a.AssignedPlayerDiscordId == character.UserDiscordId && a.CharacterId != command.CharacterId);
        if (otherCharacterOfSamePlayer)
            return Result<CommandResponse>.Fail(ResponseDetail.PlayerAlreadyAssignedInEvent, "This player already has another character assigned in this event.");

        var assignabilityResult = await raidSlotEligibilityValidator.ValidateAssignabilityAsync(raidEvent, character, command.GuildId, command.GuildBranchId, cancellationToken);
        if (assignabilityResult.IsFailed)
            return Result<CommandResponse>.Fail(assignabilityResult.Error!, assignabilityResult.Detail);

        var specResult = await ResolveSpecIdAsync(raidEvent, command.CharacterId, cancellationToken);
        if (specResult.IsFailed)
            return Result<CommandResponse>.Fail(specResult.Error!, specResult.Detail);

        await raidCompositionRepository.AssignCharacterAsync(new RaidSlotAssignment
        {
            RaidEventId = raidEvent.Id,
            GroupNumber = command.GroupNumber,
            SlotNumber = command.SlotNumber,
            CharacterId = command.CharacterId,
            SpecId = specResult.Value,
            AssignedPlayerDiscordId = character.UserDiscordId,
            AssignedAt = DateTime.UtcNow,
            AssignedByDiscordId = command.RequesterDiscordId,
        }, cancellationToken);

        if (raidEvent.PublicationStatus == RaidPublicationStatus.Published)
        {
            var raidSpecs = await characterRepository.GetRaidSpecsAsync(command.CharacterId, cancellationToken);
            var specName = raidSpecs.FirstOrDefault(s => s.SpecId == specResult.Value)?.Spec.Name;

            await raidCompositionNotifier.NotifySlotAssignedAsync(
                raidEvent, command.RequesterDiscordId,
                new RaidCharacterRef(character.Name, character.ClassId, specName),
                character.UserDiscordId,
                new SlotCoordinate(command.GroupNumber, command.SlotNumber),
                cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Character assigned successfully."));
    }

    /// <summary>
    /// Repositioning the character within the same event (drag to another slot) keeps whatever spec
    /// was already chosen for it — only a genuinely new assignment defaults to main spec, and fails
    /// if the character has none configured.
    /// </summary>
    private async Task<Result<int>> ResolveSpecIdAsync(RaidEvent raidEvent, int characterId, CancellationToken cancellationToken)
    {
        var existingAssignment = raidEvent.Assignments.FirstOrDefault(a => a.CharacterId == characterId);
        if (existingAssignment != null)
            return Result<int>.Ok(existingAssignment.SpecId);

        var raidSpecs = await characterRepository.GetRaidSpecsAsync(characterId, cancellationToken);
        var mainSpec = raidSpecs.FirstOrDefault(s => s.IsMain);
        if (mainSpec == null)
            return Result<int>.Fail(ResponseDetail.CharacterHasNoRaidSpec, "This character has no raid spec configured — set one in character settings first.");

        return Result<int>.Ok(mainSpec.SpecId);
    }
}
