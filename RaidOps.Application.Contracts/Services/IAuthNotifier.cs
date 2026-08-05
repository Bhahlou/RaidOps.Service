namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Pushes a real-time signal to a specific connected user when their Discord-sourced data
/// (roles, nickname, ...) may have changed, so the front-end can proactively re-fetch
/// <c>/user/me</c> instead of waiting on the reactive refresh-on-401 path.
/// </summary>
public interface IAuthNotifier
{
    /// <summary>
    /// Notifies every active connection for the given user that their Discord data may have
    /// changed. No diffing is performed — the caller doesn't know (and doesn't need to know)
    /// what specifically changed, only that a re-sync is worthwhile.
    /// </summary>
    /// <param name="discordId">Discord snowflake ID of the user to notify.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NotifyDiscordDataChangedAsync(string discordId, CancellationToken cancellationToken = default);
}
