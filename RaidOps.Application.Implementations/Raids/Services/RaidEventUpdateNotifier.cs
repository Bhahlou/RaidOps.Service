using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc/>
public class RaidEventUpdateNotifier(
    IRaidEventRepository raidEventRepository,
    IGuildNotificationDispatcher guildNotificationDispatcher,
    IRaidNotificationContentBuilder raidNotificationContentBuilder,
    IRaidSignupAnnouncementService raidSignupAnnouncementService,
    IRaidCompositionAnnouncementService raidCompositionAnnouncementService,
    IDiscordBotService discordBotService,
    ILogger<RaidEventUpdateNotifier> logger) : IRaidEventUpdateNotifier
{
    /// <inheritdoc/>
    public async Task MoveDedicatedChannelAsync(int eventId, int guildBranchId, RaidEvent existing, CancellationToken cancellationToken = default)
    {
        await raidSignupAnnouncementService.DeleteSignupCallAsync(existing, cancellationToken);
        await raidCompositionAnnouncementService.DeleteAnnouncementAsync(existing, cancellationToken);
        await raidEventRepository.ClearAnnouncementReferencesAsync(eventId, guildBranchId, cancellationToken);

        if (existing.DedicatedAnnouncementChannelIsBotOwned && existing.DedicatedAnnouncementChannelId is not null)
        {
            try
            {
                await discordBotService.Guilds.DeleteChannelAsync(existing.DedicatedAnnouncementChannelId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete old bot-owned dedicated channel {ChannelId} for raid event {RaidEventId} after moving it",
                    existing.DedicatedAnnouncementChannelId, eventId);
            }
        }

        if (existing.SignupMode == SignupMode.Signup)
        {
            var refreshed = await raidEventRepository.GetByIdAsync(eventId, guildBranchId, cancellationToken);
            if (refreshed is not null)
                await raidSignupAnnouncementService.PublishOrUpdateSignupCallAsync(refreshed, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyRescheduledAsync(string guildId, string requesterDiscordId, int guildBranchId, RaidEvent raidEvent, DateTime oldStartsAtUtc, CancellationToken cancellationToken = default)
    {
        var embed = await raidNotificationContentBuilder.BuildRescheduledAsync(guildId, requesterDiscordId, raidEvent, oldStartsAtUtc, cancellationToken);
        await guildNotificationDispatcher.NotifyAsync(guildId, GuildNotificationEventType.RaidRescheduled, guildBranchId, embed, cancellationToken);
    }
}
