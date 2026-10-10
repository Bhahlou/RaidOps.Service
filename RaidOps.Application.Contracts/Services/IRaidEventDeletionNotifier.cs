using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Sends every Discord notification tied to deleting a raid event — the "Raid cancelled" guild
/// notification and per-player cancellation DMs (only when the event was published), and the
/// signup-call cleanup (only when it's a <see cref="RaidOps.Domain.Enums.SignupMode.Signup"/>
/// event). Exists purely to keep the delete command handler's constructor from having to inject
/// each of the underlying notification services individually.
/// </summary>
public interface IRaidEventDeletionNotifier
{
    /// <summary>
    /// Fires the deletion notifications for <paramref name="deletedEvent"/> — it must already be
    /// deleted from the repository, the deleted snapshot is only used for its content.
    /// </summary>
    Task NotifyAsync(string guildId, string requesterDiscordId, int guildBranchId, RaidEvent deletedEvent, CancellationToken cancellationToken = default);
}
