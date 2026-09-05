using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Repository contract for persisting and reading <see cref="RaidEvent"/> occurrences.</summary>
public interface IRaidEventRepository
{
    /// <summary>Returns the event identified by <paramref name="id"/> on <paramref name="guildBranchId"/>, including its target zones and assignments, or <c>null</c> if not found.</summary>
    Task<RaidEvent?> GetByIdAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>Returns every event of <paramref name="guildBranchId"/> that starts within the given UTC range, with their target zones and assignments.</summary>
    Task<List<RaidEvent>> GetForGuildBranchInRangeAsync(int guildBranchId, DateTime rangeStartUtc, DateTime rangeEndUtc, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> if an event already exists for the given series at the given UTC start time — backs idempotent materialization.</summary>
    Task<bool> ExistsForSeriesAndDateAsync(int raidSeriesId, DateTime startsAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new event along with its target zones.</summary>
    Task<RaidEvent> AddAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the scalar fields of the event identified by <paramref name="raidEvent"/>.<see cref="RaidEvent.Id"/>
    /// and replaces its target-zone set atomically. Returns <c>false</c> if no matching event exists on <paramref name="guildBranchId"/>.
    /// </summary>
    Task<bool> UpdateAsync(RaidEvent raidEvent, int guildBranchId, IEnumerable<int> raidZoneIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-points every event whose <see cref="RaidEvent.ExtendsRaidEventId"/> is <paramref name="oldRootId"/>
    /// over to <paramref name="newRootId"/> instead. Keeps the extension-chain flattening invariant
    /// intact (every non-root event points directly at its chain's root, never at an intermediate
    /// link) when the root itself is edited to join a different chain or become standalone — a no-op
    /// when nothing currently points at <paramref name="oldRootId"/>.
    /// </summary>
    Task RepointExtensionChainAsync(int oldRootId, int? newRootId, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <see cref="RaidEvent.PublicationStatus"/> to <see cref="RaidPublicationStatus.Published"/>,
    /// stamping <see cref="RaidEvent.PublishedAt"/> (UTC now) and <see cref="RaidEvent.PublishedByDiscordId"/>.
    /// Returns <c>false</c> if no matching event exists on <paramref name="guildBranchId"/>.
    /// </summary>
    Task<bool> PublishAsync(int id, int guildBranchId, string publishedByDiscordId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the event identified by <paramref name="id"/> on <paramref name="guildBranchId"/>. Returns <c>false</c> if no matching event exists.</summary>
    Task<bool> DeleteAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the Discord channel/message ID of the standing "current composition" announcement
    /// for the given event — set on first post, or refreshed if the message had to be re-posted.
    /// </summary>
    Task UpdateCompositionAnnouncementReferenceAsync(int id, int guildBranchId, string channelId, string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the Discord channel/message ID of the standing signup-call embed for the given
    /// event — set on first post, or refreshed if the message had to be re-posted.
    /// </summary>
    Task UpdateSignupCallAnnouncementReferenceAsync(int id, int guildBranchId, string channelId, string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears both standing-embed reference pairs (composition and signup-call channel/message IDs)
    /// back to <c>null</c> — used when an event's dedicated channel changes, so the next post of
    /// either embed goes out fresh in the new channel instead of trying to edit a message that's
    /// still sitting in the old one.
    /// </summary>
    Task ClearAnnouncementReferencesAsync(int id, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk deletes every occurrence of <paramref name="raidSeriesId"/> that's still a draft with no
    /// slot assignments — used when deactivating a series to clear the empty occurrences it already
    /// produced. Published events and events with roster history are never touched. Returns the
    /// number of events deleted, and the Discord snowflake IDs of every bot-owned dedicated channel
    /// those deleted occurrences had — the caller (a bulk delete bypasses
    /// <see cref="DeleteAsync"/>'s per-event cleanup) is responsible for actually deleting those
    /// channels from Discord.
    /// </summary>
    Task<(int DeletedCount, List<string> BotOwnedChannelIds)> DeleteEmptyDraftOccurrencesForSeriesAsync(int raidSeriesId, int guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the guild's published events starting at or after <paramref name="fromUtc"/>, across
    /// every branch, earliest first — backs the Discord bot's <c>/raid invite</c> subcommand
    /// autocomplete, which has no branch context to scope by. Includes each event's branch (for its
    /// display name).
    /// </summary>
    Task<List<RaidEvent>> GetUpcomingPublishedForGuildAsync(string guildId, DateTime fromUtc, int limit, CancellationToken cancellationToken = default);
}
