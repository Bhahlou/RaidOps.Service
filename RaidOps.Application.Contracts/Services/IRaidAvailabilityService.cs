using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Resolves whether a raid roster member's declared availability blocks a raid assignment — the
/// single shared source for a rule used in two places: the single-assignment check
/// (<c>AssignCharacterToSlotCommandHandler</c>) and the raid board's bulk per-event
/// absence/availability display (<c>GetRaidBoardQueryHandler</c>). A member is blocked exactly when
/// their resolved day is <see cref="DayAvailabilityStatus.Absent"/>, or
/// <see cref="DayAvailabilityStatus.Partial"/> with a declared window that doesn't cover the event's
/// local start time.
/// </summary>
public interface IRaidAvailabilityService
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="playerDiscordId"/>'s declared availability blocks an
    /// assignment to an event starting at <paramref name="eventStartsAtUtc"/>, resolved for the
    /// (<paramref name="guildId"/>, <paramref name="guildBranchId"/>) scope in the guild's local
    /// timezone.
    /// </summary>
    Task<bool> IsPlayerUnavailableAsync(string playerDiscordId, string guildId, int guildBranchId, DateTime eventStartsAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-loads every declaration (one-off exceptions and recurring patterns, across every scope)
    /// for <paramref name="playerDiscordIds"/> overlapping <paramref name="rangeStart"/>..
    /// <paramref name="rangeEnd"/> in two queries total, then returns a lookup that resolves any of
    /// them for a specific local day without re-querying — used by the raid board to avoid one
    /// query per roster member.
    /// </summary>
    Task<IRaidAvailabilityLookup> LoadRosterAvailabilityAsync(
        IEnumerable<string> playerDiscordIds, string guildId, int guildBranchId, DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default);
}

/// <summary>
/// A pre-loaded roster availability snapshot for one (guild, guild branch) scope, resolved per
/// player/local-day without re-querying. Returned by <see cref="IRaidAvailabilityService.LoadRosterAvailabilityAsync"/>.
/// </summary>
public interface IRaidAvailabilityLookup
{
    /// <summary>The resolved status for display purposes (e.g. an assignment's availability badge).</summary>
    DayAvailabilityStatus ResolveStatus(string playerDiscordId, DateOnly localDate);

    /// <summary>Same blocking rule as <see cref="IRaidAvailabilityService.IsPlayerUnavailableAsync"/>, resolved from the pre-loaded snapshot.</summary>
    bool IsUnavailableAt(string playerDiscordId, DateOnly localDate, TimeOnly localTime);
}
