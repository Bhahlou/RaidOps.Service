using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>
/// Repository contract for <see cref="Guild"/> master-data persistence.
/// </summary>
public interface IGuildsRepository
{
    /// <summary>
    /// Returns the guild with the given <paramref name="guildId"/>, or <c>null</c> if it does not exist.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task<Guild?> GetByIdAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts new guilds or updates the name and icon hash of guilds that already exist,
    /// matching on <see cref="Guild.Id"/>.
    /// </summary>
    /// <param name="guilds">The collection of guilds to upsert.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UpsertRangeAsync(IEnumerable<Guild> guilds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the specified guild as registered in RaidOps by setting <see cref="Guild.IsRegistered"/> to <c>true</c>.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to register.</param>
    /// <param name="preferredLanguage">
    /// Best-effort language to pre-fill <see cref="Guild.Language"/> with (derived from Discord's
    /// <c>preferred_locale</c>). Only applied if the guild doesn't already have a language set —
    /// never overwrites an admin's existing choice.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The updated guild, or <c>null</c> if no matching guild exists.</returns>
    Task<Guild?> RegisterAsync(string guildId, string? preferredLanguage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the specified guild as unregistered in RaidOps by setting <see cref="Guild.IsRegistered"/> to <c>false</c>.
    /// Silently no-ops if the guild does not exist (idempotent).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to unregister.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task UnregisterAsync(string guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the guild settings (timezone, roster mode and minimum roster role) for the specified guild.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to update.</param>
    /// <param name="timezone">IANA timezone identifier (e.g. <c>"Europe/Paris"</c>).</param>
    /// <param name="rosterMode">Controls who may join the guild's roster.</param>
    /// <param name="minRosterRoleId">
    /// Discord snowflake ID of the minimum role required to join the roster.
    /// Members with this role or any role with a higher position are granted access.
    /// Only relevant when <paramref name="rosterMode"/> is <see cref="RosterMode.DiscordRoleOnly"/>.
    /// </param>
    /// <param name="language">Language RaidOps communicates in for this guild (e.g. <c>"en"</c>, <c>"fr"</c>, <c>"de"</c>).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the guild was found and updated; <c>false</c> if no matching guild exists.</returns>
    Task<bool> UpdateSettingsAsync(
        string guildId,
        string timezone,
        RosterMode rosterMode,
        string? minRosterRoleId,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only <see cref="Guild.MinOfficerRoleId"/> for the specified guild, leaving every
    /// other setting untouched — a dedicated partial update so the Officer threshold can be saved
    /// independently of the rest of guild settings (e.g. from its own settings tab).
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to update.</param>
    /// <param name="minOfficerRoleId">Discord snowflake ID of the minimum role that grants Officer access.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the guild was found and updated; <c>false</c> if no matching guild exists.</returns>
    Task<bool> UpdateOfficerThresholdAsync(
        string guildId,
        string minOfficerRoleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dev-only: fully resets a guild's registration state for replaying the get-started flow —
    /// unregisters it AND clears every setting (timezone, roster mode, role thresholds, language).
    /// Unlike <see cref="UnregisterAsync"/>, which deliberately preserves settings so a real admin
    /// re-registering later doesn't lose their configuration, this wipes them so the guild reads as
    /// genuinely unconfigured again. Silently no-ops if the guild does not exist.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to reset.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task ResetOnboardingAsync(string guildId, CancellationToken cancellationToken = default);
}
