using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="GuildNotificationSetting"/> persistence.
/// </summary>
public interface IGuildNotificationSettingsRepository
{
    /// <summary>
    /// Returns every notification setting row persisted for the given guild. Event types with no
    /// row are implicitly disabled — callers merge this against the full enum to build a complete view.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<GuildNotificationSetting>> GetAllForGuildAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the notification setting for a single (guild, event type) pair, or <c>null</c> if
    /// no row exists (i.e. the event is disabled).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="eventType">The event type to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<GuildNotificationSetting?> GetAsync(string guildId, GuildNotificationEventType eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the given settings for the specified guild, matching on
    /// (<paramref name="guildId"/>, <see cref="GuildNotificationSetting.EventType"/>).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="settings">The settings to upsert.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UpsertRangeAsync(string guildId, IEnumerable<GuildNotificationSetting> settings, CancellationToken cancellationToken = default);
}
