using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc/>
public class RaidEventDeletionNotifier(
    IGuildNotificationDispatcher guildNotificationDispatcher,
    IRaidNotificationContentBuilder raidNotificationContentBuilder,
    IRaidCompositionAnnouncementService raidCompositionAnnouncementService,
    IRaidSignupAnnouncementService raidSignupAnnouncementService) : IRaidEventDeletionNotifier
{
    /// <inheritdoc/>
    public async Task NotifyAsync(string guildId, string requesterDiscordId, int guildBranchId, RaidEvent deletedEvent, CancellationToken cancellationToken = default)
    {
        if (deletedEvent.PublicationStatus == RaidPublicationStatus.Published)
        {
            var embed = await raidNotificationContentBuilder.BuildCancelledAsync(guildId, requesterDiscordId, deletedEvent, cancellationToken);
            await guildNotificationDispatcher.NotifyAsync(guildId, GuildNotificationEventType.RaidCancelled, guildBranchId, embed, cancellationToken);

            await raidCompositionAnnouncementService.DeleteAnnouncementAsync(deletedEvent, cancellationToken);

            // Sent unconditionally (ignores the DM setting) — the only guaranteed way an assigned
            // player learns their raid was cancelled, since the public embed never pings anyone.
            foreach (var assignment in deletedEvent.Assignments)
            {
                var character = new RaidCharacterRef(assignment.Character.Name, assignment.Character.ClassId, assignment.Spec.Name);
                await raidCompositionAnnouncementService.NotifyPlayerRaidCancelledAsync(deletedEvent, assignment.AssignedPlayerDiscordId, character, cancellationToken);
            }
        }

        // The signup call is posted from creation (well before publish), so its cleanup isn't
        // gated on PublicationStatus the way composition/cancelled-notification are.
        if (deletedEvent.SignupMode == SignupMode.Signup)
            await raidSignupAnnouncementService.DeleteSignupCallAsync(deletedEvent, cancellationToken);
    }
}
