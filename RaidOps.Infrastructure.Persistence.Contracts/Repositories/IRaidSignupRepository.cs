using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for a member's <see cref="RaidSignup"/> response to a Signup-mode
/// <see cref="RaidEvent"/>. Since the composite key (RaidEventId, UserDiscordId) unambiguously
/// identifies at most one live row, writes are a single upsert rather than separate add/update
/// methods.
/// </summary>
public interface IRaidSignupRepository
{
    /// <summary>Returns the given member's response for the given event, or <c>null</c> if they haven't responded.</summary>
    Task<RaidSignup?> GetAsync(int raidEventId, string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates <paramref name="signup"/>, keyed on (RaidEventId, UserDiscordId).</summary>
    Task SetSignupAsync(RaidSignup signup, CancellationToken cancellationToken = default);

    /// <summary>Returns every response for the given event.</summary>
    Task<List<RaidSignup>> GetForEventAsync(int raidEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every response across all of <paramref name="raidEventIds"/> — a bulk counterpart to
    /// <see cref="GetForEventAsync"/> used to resolve signup eligibility for many events at once
    /// (e.g. the raid board) without one query per event.
    /// </summary>
    Task<List<RaidSignup>> GetForEventsAsync(IEnumerable<int> raidEventIds, CancellationToken cancellationToken = default);
}
