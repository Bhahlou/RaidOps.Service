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
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the guild was found and updated; <c>false</c> if no matching guild exists.</returns>
    Task<bool> RegisterAsync(string guildId, CancellationToken cancellationToken = default);

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
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the guild was found and updated; <c>false</c> if no matching guild exists.</returns>
    Task<bool> UpdateSettingsAsync(
        string guildId,
        string timezone,
        RosterMode rosterMode,
        string? minRosterRoleId,
        CancellationToken cancellationToken = default);
}
