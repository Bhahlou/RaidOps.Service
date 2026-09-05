using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidLockoutConflictChecker"/>
public class RaidLockoutConflictChecker(
    IGuildBranchesRepository guildBranchesRepository,
    IRaidZoneRepository raidZoneRepository,
    IWeeklyLockoutScheduleRepository weeklyLockoutScheduleRepository,
    IRaidLockoutService raidLockoutService,
    IRaidCompositionRepository raidCompositionRepository,
    ILogger<RaidLockoutConflictChecker> logger) : IRaidLockoutConflictChecker
{
    /// <inheritdoc/>
    public async Task<string?> FindConflictingZoneNameAsync(RaidEvent raidEvent, int characterId, string guildId, int guildBranchId, CancellationToken cancellationToken = default)
    {
        var targetZoneIds = raidEvent.TargetZones.Select(z => z.RaidZoneId).ToList();
        if (targetZoneIds.Count == 0)
            return null;

        var guildBranch = await guildBranchesRepository.GetByIdAsync(guildBranchId, cancellationToken);
        var zones = await raidZoneRepository.GetByIdsAsync(targetZoneIds, cancellationToken);
        var guildOverrides = await raidZoneRepository.GetGuildOverridesAsync(guildId, targetZoneIds, cancellationToken);
        var guildOverridesByZone = guildOverrides.ToDictionary(o => o.RaidZoneId);
        var otherAssignments = await raidCompositionRepository.GetActiveAssignmentsForCharacterInGuildBranchAsync(characterId, guildBranchId, cancellationToken);
        var extensionGroupKey = raidEvent.ExtendsRaidEventId ?? raidEvent.Id;

        foreach (var zone in zones)
        {
            guildOverridesByZone.TryGetValue(zone.Id, out var guildOverride);
            var conflictingZoneName = await FindConflictInZoneAsync(zone, guildOverride, guildBranch, guildBranchId, raidEvent, extensionGroupKey, otherAssignments, cancellationToken);
            if (conflictingZoneName != null)
                return conflictingZoneName;
        }

        return null;
    }

    /// <summary>
    /// Checks a single target zone for a lockout-window collision against <paramref name="otherAssignments"/>,
    /// returning <paramref name="zone"/>'s name on the first conflict found or <c>null</c> if none —
    /// factored out of <see cref="FindConflictingZoneNameAsync"/> purely to keep both methods'
    /// cognitive complexity down, no behavior change.
    /// </summary>
    private async Task<string?> FindConflictInZoneAsync(
        RaidZone zone, GuildRaidZoneLockout? guildOverride, GuildBranch? guildBranch, int guildBranchId,
        RaidEvent raidEvent, int extensionGroupKey, List<RaidSlotAssignment> otherAssignments, CancellationToken cancellationToken)
    {
        var baseline = await ResolveLockoutBaselineAsync(zone, guildBranch, guildOverride, cancellationToken);
        if (baseline == null)
        {
            // No independent cadence on the zone and no region configured on the guild branch —
            // nothing to compare against. Soft-skip rather than block every assignment, same as
            // the guild-timezone fallback elsewhere; log so the gap is visible to fix.
            logger.LogWarning(
                "Skipping lockout check for zone {ZoneId} on guild branch {GuildBranchId} — no independent cadence and no region configured.",
                zone.Id, guildBranchId);
            return null;
        }

        var overrides = zone.LockoutOverrides.ToList();
        var thisWindowStart = raidLockoutService.GetLockoutWindowStart(baseline.Value.AnchorUtc, baseline.Value.CadenceDays, overrides, raidEvent.StartsAtUtc);

        foreach (var other in otherAssignments)
        {
            if (other.RaidEventId == raidEvent.Id)
                continue; // same event — not a cross-event conflict

            if ((other.RaidEvent.ExtendsRaidEventId ?? other.RaidEvent.Id) == extensionGroupKey)
                continue; // same extension chain — sharing the lockout window is intentional

            if (!other.RaidEvent.TargetZones.Any(z => z.RaidZoneId == zone.Id))
                continue;

            var otherWindowStart = raidLockoutService.GetLockoutWindowStart(baseline.Value.AnchorUtc, baseline.Value.CadenceDays, overrides, other.RaidEvent.StartsAtUtc);

            if (otherWindowStart == thisWindowStart)
                return zone.Name;
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
