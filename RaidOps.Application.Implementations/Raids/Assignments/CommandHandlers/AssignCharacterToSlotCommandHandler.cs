using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Assignments.CommandHandlers;

/// <summary>
/// Handles <see cref="AssignCharacterToSlotCommand"/> — the central validation point of the raid
/// builder. Checks run in a fixed order (event state, roster eligibility on this specific guild
/// branch, grid bounds/occupancy, one-character-per-player, declared absence, lockout conflict) so
/// the first failing rule always produces the same, predictable error for a given bad request. A
/// drop onto an already-occupied slot is rejected outright — no automatic swap.
/// </summary>
public class AssignCharacterToSlotCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IAvailabilityRepository availabilityRepository,
    IAvailabilityResolutionService availabilityResolutionService,
    IRaidZoneRepository raidZoneRepository,
    IRaidLockoutService raidLockoutService,
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

        if (raidEvent.Status == RaidEventStatus.Cancelled)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventCancelled, "Cannot assign to a cancelled raid event.");

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

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        var eventLocalDateTime = GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild?.Timezone);
        var eventLocalDate = DateOnly.FromDateTime(eventLocalDateTime);

        var absenceFailure = await CheckDeclaredAbsenceAsync(character.UserDiscordId, command.GuildId, command.GuildBranchId, eventLocalDate, TimeOnly.FromDateTime(eventLocalDateTime), cancellationToken);
        if (absenceFailure != null)
            return absenceFailure;

        var lockoutFailure = await CheckLockoutConflictAsync(raidEvent, command.CharacterId, command.GuildId, command.GuildBranchId, eventLocalDate, guild?.Timezone, cancellationToken);
        if (lockoutFailure != null)
            return lockoutFailure;

        await raidCompositionRepository.AssignCharacterAsync(new RaidSlotAssignment
        {
            RaidEventId = raidEvent.Id,
            GroupNumber = command.GroupNumber,
            SlotNumber = command.SlotNumber,
            CharacterId = command.CharacterId,
            AssignedPlayerDiscordId = character.UserDiscordId,
            AssignedAt = DateTime.UtcNow,
            AssignedByDiscordId = command.RequesterDiscordId,
        }, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Character assigned successfully."));
    }

    /// <summary>
    /// Absence is a hard block: <see cref="DayAvailabilityStatus.Absent"/> always rejects, and
    /// <see cref="DayAvailabilityStatus.Partial"/> rejects only when the event's local start time
    /// falls outside the declared window. Fetches the player's declarations across every scope
    /// (Global and every branch), then resolves authoritatively for this specific guild branch via
    /// <see cref="IAvailabilityResolutionService.ResolveForScope"/> — branch-scoped declarations win
    /// over Global ones, per the resolution cascade.
    /// </summary>
    private async Task<Result<CommandResponse>?> CheckDeclaredAbsenceAsync(
        string playerDiscordId, string guildId, int guildBranchId, DateOnly eventLocalDate, TimeOnly eventLocalTime, CancellationToken cancellationToken)
    {
        var memberExceptions = await availabilityRepository.GetExceptionsOverlappingAsync(playerDiscordId, eventLocalDate, eventLocalDate, cancellationToken);
        var memberPatterns = await availabilityRepository.GetPatternsAsync(playerDiscordId, cancellationToken);
        var resolvedDay = availabilityResolutionService.ResolveForScope(eventLocalDate, eventLocalDate, memberExceptions, memberPatterns, guildId, guildBranchId)[0];

        if (resolvedDay.Status == DayAvailabilityStatus.Absent)
            return Result<CommandResponse>.Fail(ResponseDetail.MemberDeclaredAbsent, "This member declared themselves absent on the event's date.");

        if (resolvedDay.Status == DayAvailabilityStatus.Partial)
        {
            var withinWindow =
                (resolvedDay.AvailableFrom == null || eventLocalTime >= resolvedDay.AvailableFrom.Value) &&
                (resolvedDay.AvailableUntil == null || eventLocalTime <= resolvedDay.AvailableUntil.Value);

            if (!withinWindow)
                return Result<CommandResponse>.Fail(ResponseDetail.MemberDeclaredAbsent, "This member's declared partial availability does not cover the event's start time.");
        }

        return null;
    }

    /// <summary>
    /// Two events conflict for this character on a shared zone iff the lockout engine resolves
    /// the same window-start date for both. Scoped to the guild only — a character has at most one
    /// active guild membership.
    /// </summary>
    private async Task<Result<CommandResponse>?> CheckLockoutConflictAsync(
        RaidEvent raidEvent, int characterId, string guildId, int guildBranchId, DateOnly eventLocalDate, string? guildTimezone, CancellationToken cancellationToken)
    {
        var targetZoneIds = raidEvent.TargetZones.Select(z => z.RaidZoneId).ToList();
        if (targetZoneIds.Count == 0)
            return null;

        var zones = await raidZoneRepository.GetByIdsAsync(targetZoneIds, cancellationToken);
        var guildOverrides = await raidZoneRepository.GetGuildOverridesAsync(guildId, targetZoneIds, cancellationToken);
        var guildOverridesByZone = guildOverrides.ToDictionary(o => o.RaidZoneId);
        var otherAssignments = await raidCompositionRepository.GetActiveAssignmentsForCharacterInGuildBranchAsync(characterId, guildBranchId, cancellationToken);

        foreach (var zone in zones)
        {
            // A guild-specific correction (e.g. a different region reset day) overrides the zone's
            // shared baseline for this guild only; time-bound `overrides` remain zone-wide (a
            // temporary anomaly affects every guild running that content, not just one).
            guildOverridesByZone.TryGetValue(zone.Id, out var guildOverride);
            var baselineAnchorDate = guildOverride?.LockoutAnchorDate ?? zone.LockoutAnchorDate;
            var baselineCadenceDays = guildOverride?.LockoutCadenceDays ?? zone.LockoutCadenceDays;

            var overrides = zone.LockoutOverrides.ToList();
            var thisWindowStart = raidLockoutService.GetLockoutWindowStart(baselineAnchorDate, baselineCadenceDays, overrides, eventLocalDate);

            foreach (var other in otherAssignments)
            {
                if (other.RaidEventId == raidEvent.Id)
                    continue; // same event — not a cross-event conflict

                if (!other.RaidEvent.TargetZones.Any(z => z.RaidZoneId == zone.Id))
                    continue;

                var otherLocalDate = GuildTimeHelper.ToGuildLocalDate(other.RaidEvent.StartsAtUtc, guildTimezone);
                var otherWindowStart = raidLockoutService.GetLockoutWindowStart(baselineAnchorDate, baselineCadenceDays, overrides, otherLocalDate);

                if (otherWindowStart == thisWindowStart)
                    return Result<CommandResponse>.Fail(ResponseDetail.RaidLockoutConflict, $"Character is already locked to '{zone.Name}' for this reset window via another event.");
            }
        }

        return null;
    }
}
