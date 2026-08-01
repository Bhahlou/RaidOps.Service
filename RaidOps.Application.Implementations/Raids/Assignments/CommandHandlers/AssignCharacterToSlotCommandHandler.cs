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
/// <see cref="SwapSlotAssignmentsCommandHandler"/> for exchanging two occupied slots.
/// </summary>
public class AssignCharacterToSlotCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IRaidAvailabilityService raidAvailabilityService,
    IRaidLockoutConflictChecker raidLockoutConflictChecker,
    IRaidCompositionRepository raidCompositionRepository) : ICommandHandlerAsync<AssignCharacterToSlotCommand>
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

        var memberships = await guildMembershipRepository.GetByCharacterIdAsync(command.CharacterId, cancellationToken);
        if (!memberships.Any(m => m.GuildBranchId == command.GuildBranchId))
            return Result<CommandResponse>.Fail(ResponseDetail.CharacterNotOnRoster, "Character is not an active member of this guild branch's roster.");

        if (command.GroupNumber < 1 || command.GroupNumber > raidEvent.GroupCount || command.SlotNumber < 1 || command.SlotNumber > raidEvent.SlotsPerGroup)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidGroupOrSlotNumber, "Group/slot number is out of the event's grid bounds.");

        var slotOccupant = raidEvent.Assignments.FirstOrDefault(a => a.GroupNumber == command.GroupNumber && a.SlotNumber == command.SlotNumber);
        if (slotOccupant != null && slotOccupant.CharacterId != command.CharacterId)
            return Result<CommandResponse>.Fail(ResponseDetail.SlotOccupied, "This slot is already occupied — unassign it first.");

        var otherCharacterOfSamePlayer = raidEvent.Assignments.Any(a => a.AssignedPlayerDiscordId == character.UserDiscordId && a.CharacterId != command.CharacterId);
        if (otherCharacterOfSamePlayer)
            return Result<CommandResponse>.Fail(ResponseDetail.PlayerAlreadyAssignedInEvent, "This player already has another character assigned in this event.");

        var isUnavailable = await raidAvailabilityService.IsPlayerUnavailableAsync(character.UserDiscordId, command.GuildId, command.GuildBranchId, raidEvent.StartsAtUtc, cancellationToken);
        if (isUnavailable)
            return Result<CommandResponse>.Fail(ResponseDetail.MemberDeclaredAbsent, "This member's declared availability does not cover the event's start time.");

        var conflictingZoneName = await raidLockoutConflictChecker.FindConflictingZoneNameAsync(raidEvent, command.CharacterId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (conflictingZoneName != null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidLockoutConflict, $"Character is already locked to '{conflictingZoneName}' for this reset window via another event.");

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
