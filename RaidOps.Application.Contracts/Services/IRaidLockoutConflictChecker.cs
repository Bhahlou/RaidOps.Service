using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Detects whether assigning a character to a raid event would conflict with a lockout it already
/// holds via another event on a shared raid zone. Two events conflict for a character on a shared
/// zone iff <see cref="IRaidLockoutService"/> resolves the same window-start instant for both,
/// compared directly on their UTC start times.
/// </summary>
public interface IRaidLockoutConflictChecker
{
    /// <summary>
    /// Returns the display name of the first raid zone <paramref name="raidEvent"/> shares a lockout
    /// conflict on for <paramref name="characterId"/>, or <c>null</c> if there is none.
    /// </summary>
    Task<string?> FindConflictingZoneNameAsync(RaidEvent raidEvent, int characterId, string guildId, int guildBranchId, CancellationToken cancellationToken = default);
}
