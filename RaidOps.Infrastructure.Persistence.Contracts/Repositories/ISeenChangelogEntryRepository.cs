namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for the per-user ledger of acknowledged front-end changelog entries.
/// </summary>
public interface ISeenChangelogEntryRepository
{
    /// <summary>
    /// Returns the set of changelog entry ids the given user has acknowledged.
    /// </summary>
    /// <param name="userDiscordId">Discord snowflake ID of the user.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<HashSet<string>> GetSeenEntryIdsAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the given user acknowledged the given changelog entries. Idempotent — entries
    /// already recorded as seen are skipped.
    /// </summary>
    /// <param name="userDiscordId">Discord snowflake ID of the user.</param>
    /// <param name="entryIds">The changelog entry ids to mark as seen.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task MarkSeenAsync(string userDiscordId, IEnumerable<string> entryIds, CancellationToken cancellationToken = default);
}
