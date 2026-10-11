namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for the per-user ledger of completed or dismissed front-end guided tours.
/// </summary>
public interface ISeenTourRepository
{
    /// <summary>
    /// Returns the set of tour ids the given user has already seen.
    /// </summary>
    /// <param name="userDiscordId">Discord snowflake ID of the user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<HashSet<string>> GetSeenTourIdsAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the given user has seen the given tour. Idempotent — a tour already recorded
    /// as seen is skipped.
    /// </summary>
    /// <param name="userDiscordId">Discord snowflake ID of the user.</param>
    /// <param name="tourId">The tour id to mark as seen.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task MarkSeenAsync(string userDiscordId, string tourId, CancellationToken cancellationToken = default);
}
