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
    /// Returns the notification setting for a (guild, event type) pair, scoped to
    /// <paramref name="guildBranchId"/> when given: the branch-specific override row is tried first,
    /// falling back to the guild-wide row (<see cref="GuildNotificationSetting.GuildBranchId"/> <c>null</c>)
    /// when no branch-specific row exists. Passing <c>null</c> looks up the guild-wide row directly.
    /// Returns <c>null</c> if no matching row exists (i.e. the event is disabled for this scope).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="eventType">The event type to look up.</param>
    /// <param name="guildBranchId">The branch to check for an override, or <c>null</c> for the guild-wide row directly.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<GuildNotificationSetting?> GetAsync(string guildId, GuildNotificationEventType eventType, int? guildBranchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the given settings for the specified guild, matching on
    /// (<paramref name="guildId"/>, <see cref="GuildNotificationSetting.EventType"/>).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="settings">The settings to upsert.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UpsertRangeAsync(string guildId, IEnumerable<GuildNotificationSetting> settings, CancellationToken cancellationToken = default);
}
