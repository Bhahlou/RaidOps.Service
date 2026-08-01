using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Assignments.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
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
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IRaidEventRepository raidEventRepository,
    ICharacterRepository characterRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IAvailabilityRepository availabilityRepository,
    IAvailabilityResolutionService availabilityResolutionService,
    IRaidZoneRepository raidZoneRepository,
    IWeeklyLockoutScheduleRepository weeklyLockoutScheduleRepository,
    IRaidLockoutService raidLockoutService,
    IRaidCompositionRepository raidCompositionRepository,
    ILogger<AssignCharacterToSlotCommandHandler> logger) : ICommandHandlerAsync<AssignCharacterToSlotCommand>
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

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        var eventLocalDateTime = GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild?.Timezone);
        var eventLocalDate = DateOnly.FromDateTime(eventLocalDateTime);

        var absenceFailure = await CheckDeclaredAbsenceAsync(character.UserDiscordId, command.GuildId, command.GuildBranchId, eventLocalDate, TimeOnly.FromDateTime(eventLocalDateTime), cancellationToken);
        if (absenceFailure != null)
            return absenceFailure;

        var lockoutFailure = await CheckLockoutConflictAsync(raidEvent, command.CharacterId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (lockoutFailure != null)
            return lockoutFailure;

        // Repositioning the character within the same event (drag to another slot) keeps whatever
        // spec was already chosen for it — only a genuinely new assignment defaults to main spec.
        var existingAssignment = raidEvent.Assignments.FirstOrDefault(a => a.CharacterId == command.CharacterId);
        int specId;
        if (existingAssignment != null)
        {
            specId = existingAssignment.SpecId;
        }
        else
        {
            var raidSpecs = await characterRepository.GetRaidSpecsAsync(command.CharacterId, cancellationToken);
            var mainSpec = raidSpecs.FirstOrDefault(s => s.IsMain);
            if (mainSpec == null)
                return Result<CommandResponse>.Fail(ResponseDetail.CharacterHasNoRaidSpec, "This character has no raid spec configured — set one in character settings first.");

            specId = mainSpec.SpecId;
        }

        await raidCompositionRepository.AssignCharacterAsync(new RaidSlotAssignment
        {
            RaidEventId = raidEvent.Id,
            GroupNumber = command.GroupNumber,
            SlotNumber = command.SlotNumber,
            CharacterId = command.CharacterId,
            SpecId = specId,
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
    /// Two events conflict for this character on a shared zone iff the lockout engine resolves the
    /// same window-start instant for both, compared directly on their UTC start times — the real
    /// reset is a fixed UTC instant (e.g. Wednesday 04:00 UTC for the EU region), not a guild-local
    /// calendar day, so no timezone conversion belongs in this comparison. Scoped to the guild only
    /// — a character has at most one active guild membership.
    /// </summary>
    private async Task<Result<CommandResponse>?> CheckLockoutConflictAsync(
        RaidEvent raidEvent, int characterId, string guildId, int guildBranchId, CancellationToken cancellationToken)
    {
        var targetZoneIds = raidEvent.TargetZones.Select(z => z.RaidZoneId).ToList();
        if (targetZoneIds.Count == 0)
            return null;

        var guildBranch = await guildBranchesRepository.GetByIdAsync(guildBranchId, cancellationToken);
        var zones = await raidZoneRepository.GetByIdsAsync(targetZoneIds, cancellationToken);
        var guildOverrides = await raidZoneRepository.GetGuildOverridesAsync(guildId, targetZoneIds, cancellationToken);
        var guildOverridesByZone = guildOverrides.ToDictionary(o => o.RaidZoneId);
        var otherAssignments = await raidCompositionRepository.GetActiveAssignmentsForCharacterInGuildBranchAsync(characterId, guildBranchId, cancellationToken);

        foreach (var zone in zones)
        {
            guildOverridesByZone.TryGetValue(zone.Id, out var guildOverride);
            var baseline = await ResolveLockoutBaselineAsync(zone, guildBranch, guildOverride, cancellationToken);
            if (baseline == null)
            {
                // No independent cadence on the zone and no region configured on the guild branch —
                // nothing to compare against. Soft-skip rather than block every assignment, same as
                // the guild-timezone fallback elsewhere; log so the gap is visible to fix.
                logger.LogWarning(
                    "Skipping lockout check for zone {ZoneId} on guild branch {GuildBranchId} — no independent cadence and no region configured.",
                    zone.Id, guildBranchId);
                continue;
            }

            var overrides = zone.LockoutOverrides.ToList();
            var thisWindowStart = raidLockoutService.GetLockoutWindowStart(baseline.Value.AnchorUtc, baseline.Value.CadenceDays, overrides, raidEvent.StartsAtUtc);

            foreach (var other in otherAssignments)
            {
                if (other.RaidEventId == raidEvent.Id)
                    continue; // same event — not a cross-event conflict

                if (!other.RaidEvent.TargetZones.Any(z => z.RaidZoneId == zone.Id))
                    continue;

                var otherWindowStart = raidLockoutService.GetLockoutWindowStart(baseline.Value.AnchorUtc, baseline.Value.CadenceDays, overrides, other.RaidEvent.StartsAtUtc);

                if (otherWindowStart == thisWindowStart)
                    return Result<CommandResponse>.Fail(ResponseDetail.RaidLockoutConflict, $"Character is already locked to '{zone.Name}' for this reset window via another event.");
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the (anchor, cadence) baseline for a zone's lockout window: the zone's own
    /// independent cadence if it has one (e.g. Vanilla's Zul'Gurub/AQ20 every 3 days), otherwise the
    /// guild branch's regional <see cref="WeeklyLockoutSchedule"/>. A per-guild
    /// <see cref="GuildRaidZoneLockout"/> correction, when present, overrides either resolution on a
    /// per-field basis. Returns <c>null</c> when nothing can be resolved (no independent cadence and
    /// no region configured on the branch).
    /// </summary>
    private async Task<(DateTime AnchorUtc, int CadenceDays)?> ResolveLockoutBaselineAsync(
        RaidZone zone, GuildBranch? guildBranch, GuildRaidZoneLockout? guildOverride, CancellationToken cancellationToken)
    {
        DateTime baselineAnchorUtc;
        int baselineCadenceDays;

        if (zone.LockoutCadenceDays.HasValue && zone.LockoutAnchorUtc.HasValue)
        {
            baselineAnchorUtc = zone.LockoutAnchorUtc.Value;
            baselineCadenceDays = zone.LockoutCadenceDays.Value;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(guildBranch?.Region))
                return null;

            var schedule = await weeklyLockoutScheduleRepository.GetByRegionAsync(guildBranch.Region, cancellationToken);
            if (schedule == null)
                return null;

            baselineAnchorUtc = schedule.AnchorUtc;
            baselineCadenceDays = schedule.CadenceDays;
        }

        return (
            guildOverride?.LockoutAnchorUtc ?? baselineAnchorUtc,
            guildOverride?.LockoutCadenceDays ?? baselineCadenceDays);
    }
}
