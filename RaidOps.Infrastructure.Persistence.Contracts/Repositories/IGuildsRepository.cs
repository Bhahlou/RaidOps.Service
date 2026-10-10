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
    /// Persists the guild-level identity settings (timezone and language) for the specified guild.
    /// Roster/officer role-set configuration lives per-branch now — see
    /// <see cref="IGuildBranchesRepository.UpdateRosterSettingsAsync"/>.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to update.</param>
    /// <param name="timezone">IANA timezone identifier (e.g. <c>"Europe/Paris"</c>).</param>
    /// <param name="language">Language RaidOps communicates in for this guild (e.g. <c>"en"</c>, <c>"fr"</c>, <c>"de"</c>).</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the guild was found and updated; <c>false</c> if no matching guild exists.</returns>
    Task<bool> UpdateSettingsAsync(
        string guildId,
        string timezone,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dev-only: fully resets a guild's registration state for replaying the get-started flow —
    /// unregisters it AND clears every guild-level setting (timezone, language). Branch roster
    /// settings are left untouched; the get-started flow re-runs the branch step separately.
    /// Unlike <see cref="UnregisterAsync"/>, which deliberately preserves settings so a real admin
    /// re-registering later doesn't lose their configuration, this wipes them so the guild reads as
    /// genuinely unconfigured again. Silently no-ops if the guild does not exist.
    /// </summary>
    /// <param name="guildId">The Discord snowflake ID of the guild to reset.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task ResetOnboardingAsync(string guildId, CancellationToken cancellationToken = default);
}
