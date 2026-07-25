using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Computes lockout reset windows for a raid zone. Two events are in lockout conflict for a
/// character on a shared zone if and only if this service returns the same window-start date for
/// both events' local dates.
/// </summary>
public interface IRaidLockoutService
{
    /// <summary>
    /// Returns the start date of the lockout window that covers <paramref name="date"/>, honoring
    /// any <paramref name="overrides"/> that were active over part of the period between
    /// <paramref name="baselineAnchorDate"/> and <paramref name="date"/>. Uses whole-cadence integer
    /// jumps rather than a day-by-day loop, so long constant-cadence spans resolve in a handful of
    /// steps regardless of how far back the anchor date is.
    /// </summary>
    /// <param name="baselineAnchorDate">
    /// Reference reset date used as the origin for the computation — the zone's seeded
    /// <c>RaidZone.LockoutAnchorDate</c>, or a guild's <c>GuildRaidZoneLockout</c> correction when
    /// one exists (e.g. a different region reset day). Any genuine reset day works as the caller
    /// already resolved which one applies.
    /// </param>
    /// <param name="baselineCadenceDays">
    /// Baseline cadence in days — the zone's seeded <c>RaidZone.LockoutCadenceDays</c>, or a guild's
    /// <c>GuildRaidZoneLockout</c> correction when one exists.
    /// </param>
    /// <param name="overrides">Time-bound, zone-wide cadence corrections shared by every guild (e.g. a temporary anomaly); may be empty.</param>
    /// <param name="date">The date to resolve the covering lockout window for.</param>
    DateOnly GetLockoutWindowStart(DateOnly baselineAnchorDate, int baselineCadenceDays, IReadOnlyCollection<RaidLockoutCadenceOverride> overrides, DateOnly date);
}
