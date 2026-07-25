using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Notifications;

/// <summary>
/// Distinct from <see cref="RaidOps.Application.Implementations.Notifications"/>, which drives the
/// in-app notification bell — this dispatcher posts to Discord.
/// </summary>
public class GuildNotificationDispatcher(
    IGuildNotificationSettingsRepository notificationSettingsRepository,
    IDiscordBotService discordBotService,
    ILogger<GuildNotificationDispatcher> logger) : IGuildNotificationDispatcher
{
    /// <inheritdoc/>
    public async Task NotifyAsync(string guildId, GuildNotificationEventType eventType, DiscordEmbedContent embed, CancellationToken cancellationToken = default)
    {
        var setting = await notificationSettingsRepository.GetAsync(guildId, eventType, cancellationToken);
        if (setting is not { Enabled: true, ChannelId: not null })
            return;

        try
        {
            await discordBotService.Messages.SendEmbedAsync(ulong.Parse(setting.ChannelId), embed, cancellationToken);
        }
        catch (Exception ex)
        {
            // A failed Discord post (missing permissions, deleted channel, bot down, ...) must
            // never fail the domain command that triggered it.
            logger.LogWarning(
                ex,
                "Failed to post Discord notification for guild {GuildId}, event {EventType}, channel {ChannelId}",
                guildId, eventType, setting.ChannelId);
        }
    }
}
