using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidSignupAnnouncementService"/>
public class RaidSignupAnnouncementService(
    IGuildNotificationSettingsRepository notificationSettingsRepository,
    IRaidEventRepository raidEventRepository,
    IRaidNotificationContentBuilder contentBuilder,
    IDiscordBotService discordBotService,
    IRaidSignupResponseBuilder raidSignupResponseBuilder,
    ILogger<RaidSignupAnnouncementService> logger) : IRaidSignupAnnouncementService
{
    /// <inheritdoc/>
    public async Task PublishOrUpdateSignupCallAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        // An explicit per-raid channel choice is itself the opt-in, independent of the guild-wide toggle.
        string? resolvedChannelId = raidEvent.DedicatedAnnouncementChannelId;
        if (resolvedChannelId is null)
        {
            var setting = await notificationSettingsRepository.GetAsync(
                raidEvent.GuildId, GuildNotificationEventType.RaidSignupCallPosted, raidEvent.GuildBranchId, cancellationToken);
            if (setting is not { Enabled: true, ChannelId: not null })
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Skipping signup call for raid event {RaidEventId}: no dedicated channel on the event and no guild-wide RaidSignupCallPosted channel configured for branch {GuildBranchId}.",
                        raidEvent.Id, raidEvent.GuildBranchId);
                }
                return;
            }

            resolvedChannelId = setting.ChannelId;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Posting/updating signup call for raid event {RaidEventId} to channel {ChannelId} (dedicated: {IsDedicated}).",
                raidEvent.Id, resolvedChannelId, raidEvent.DedicatedAnnouncementChannelId is not null);
        }

        try
        {
            var signups = await raidSignupResponseBuilder.BuildAsync(raidEvent, cancellationToken);
            var embed = await contentBuilder.BuildSignupCallAsync(raidEvent.GuildId, raidEvent.GuildBranchId, raidEvent, signups, cancellationToken);
            var channelId = ulong.Parse(resolvedChannelId);

            if (raidEvent.SignupCallAnnouncementChannelId is null || raidEvent.SignupCallAnnouncementMessageId is null)
            {
                var messageId = await discordBotService.Messages.PostEmbedAsync(channelId, embed, cancellationToken);
                await raidEventRepository.UpdateSignupCallAnnouncementReferenceAsync(
                    raidEvent.Id, raidEvent.GuildBranchId, channelId.ToString(), messageId.ToString(), cancellationToken);
            }
            else
            {
                await discordBotService.Messages.EditEmbedAsync(
                    ulong.Parse(raidEvent.SignupCallAnnouncementChannelId),
                    ulong.Parse(raidEvent.SignupCallAnnouncementMessageId),
                    embed, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to post/update signup call for raid event {RaidEventId} in guild {GuildId}",
                raidEvent.Id, raidEvent.GuildId);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteSignupCallAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        if (raidEvent.SignupCallAnnouncementChannelId is null || raidEvent.SignupCallAnnouncementMessageId is null)
            return;

        try
        {
            await discordBotService.Messages.DeleteMessageAsync(
                ulong.Parse(raidEvent.SignupCallAnnouncementChannelId),
                ulong.Parse(raidEvent.SignupCallAnnouncementMessageId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to delete signup call for raid event {RaidEventId} in guild {GuildId}",
                raidEvent.Id, raidEvent.GuildId);
        }
    }
}
