using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="GuildNotificationSetting"/> persistence.
/// </summary>
public interface IGuildNotificationSettingsRepository
{
    /// <summary>
    /// Returns every notification setting row persisted for the given guild, across every branch and
    /// the guild-wide fallback. Event types with no row anywhere are implicitly disabled — callers
    /// merge this against the full enum to build a complete view. Use this for guild-wide "has any
    /// row been saved at all" checks; use <see cref="GetEffectiveForGuildAsync"/> to resolve the
    /// settings actually in effect for one specific scope.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<GuildNotificationSetting>> GetAllForGuildAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the effective notification setting row per event type for the given scope: the
    /// branch-specific override row when one exists for <paramref name="guildBranchId"/>, falling
    /// back to the guild-wide row (<see cref="GuildNotificationSetting.GuildBranchId"/> <c>null</c>)
    /// otherwise. Passing <c>null</c> returns only guild-wide rows. Event types with no matching row
    /// in either scope are absent from the result — callers merge this against the full enum.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="guildBranchId">The branch to resolve, or <c>null</c> for the guild-wide scope.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<GuildNotificationSetting>> GetEffectiveForGuildAsync(string guildId, int? guildBranchId, CancellationToken cancellationToken = default);

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
    /// Inserts or updates the given settings for the specified guild and scope, matching on
    /// (<paramref name="guildId"/>, <paramref name="guildBranchId"/>, <see cref="GuildNotificationSetting.EventType"/>).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild.</param>
    /// <param name="guildBranchId">The branch this batch overrides, or <c>null</c> for the guild-wide row.</param>
    /// <param name="settings">The settings to upsert.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UpsertRangeAsync(string guildId, int? guildBranchId, IEnumerable<GuildNotificationSetting> settings, CancellationToken cancellationToken = default);
}
