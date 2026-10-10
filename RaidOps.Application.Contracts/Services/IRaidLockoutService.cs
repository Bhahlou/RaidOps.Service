using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Computes lockout reset windows for a raid zone. Two events are in lockout conflict for a
/// character on a shared zone if and only if this service returns the same window-start instant for
/// both events' UTC start times.
/// </summary>
public interface IRaidLockoutService
{
    /// <summary>
    /// Returns the start instant (UTC) of the lockout window that covers <paramref name="utcInstant"/>,
    /// honoring any <paramref name="overrides"/> that were active over part of the period between
    /// <paramref name="baselineAnchorUtc"/> and <paramref name="utcInstant"/>. Uses whole-cadence
    /// integer jumps rather than a day-by-day loop, so long constant-cadence spans resolve in a
    /// handful of steps regardless of how far back the anchor is. Deliberately UTC-only — the
    /// real-world reset is a fixed UTC instant (e.g. Wednesday 04:00 UTC for the EU region), not a
    /// guild-local calendar day, so no timezone conversion belongs in this computation.
    /// </summary>
    /// <param name="baselineAnchorUtc">
    /// Reference reset instant (UTC) used as the origin for the computation — the zone's own
    /// <c>RaidZone.LockoutAnchorUtc</c> when it has an independent cadence, otherwise the guild
    /// branch's <c>WeeklyLockoutSchedule.AnchorUtc</c> (via its <c>Region</c>), or a guild's
    /// <c>GuildRaidZoneLockout</c> correction when one exists. Any genuine reset instant works, as
    /// the caller already resolved which one applies.
    /// </param>
    /// <param name="baselineCadenceDays">
    /// Baseline cadence in days — the zone's own <c>RaidZone.LockoutCadenceDays</c>, the region's
    /// <c>WeeklyLockoutSchedule.CadenceDays</c> (always 7 today), or a guild's
    /// <c>GuildRaidZoneLockout</c> correction when one exists.
    /// </param>
    /// <param name="overrides">Time-bound, zone-wide cadence corrections shared by every guild (e.g. a temporary anomaly); may be empty.</param>
    /// <param name="utcInstant">The UTC instant to resolve the covering lockout window for.</param>
    DateTime GetLockoutWindowStart(DateTime baselineAnchorUtc, int baselineCadenceDays, IReadOnlyCollection<RaidLockoutCadenceOverride> overrides, DateTime utcInstant);
}
