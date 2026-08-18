namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Pushes a real-time signal to every connection currently watching a Signup-mode raid event's
/// board, whenever a roster member's response changes — whether they responded from the web or
/// from the Discord signup-call embed's buttons. No payload beyond the event ID is sent — the
/// front-end re-fetches via <c>GetRaidSignupsQuery</c> instead of trusting a pushed DTO.
/// </summary>
public interface IRaidSignupNotifier
{
    /// <summary>
    /// Notifies every connection joined to <paramref name="eventId"/>'s group that a signup
    /// response changed. No diffing is performed — the caller doesn't know (and doesn't need to
    /// know) whose response changed, only that a re-sync is worthwhile.
    /// </summary>
    /// <param name="guildBranchId">Surrogate ID of the guild branch the event belongs to.</param>
    /// <param name="eventId">ID of the raid event whose signups changed.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NotifyRaidSignupChangedAsync(int guildBranchId, int eventId, CancellationToken cancellationToken = default);
}
