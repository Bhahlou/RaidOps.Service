using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Records notable guild actions to the audit log for the activity feed.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Appends an entry to the guild's audit log.
    /// </summary>
    /// <param name="guildId">Discord snowflake ID of the guild.</param>
    /// <param name="actorDiscordId">Discord snowflake ID of the user who triggered the action.</param>
    /// <param name="action">Category of the action.</param>
    /// <param name="variables">
    /// Optional key-value pairs serialized as JSON into <c>Details</c>.
    /// The front-end uses these to interpolate i18n templates keyed on <paramref name="action"/>.
    /// Example: <c>{ ["characterName"] = "Arthas" }</c>.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task LogAsync(
        string guildId,
        string actorDiscordId,
        GuildAuditAction action,
        Dictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default);
}
