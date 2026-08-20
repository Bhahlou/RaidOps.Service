using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidCompositionAnnouncementService"/>
public class RaidCompositionAnnouncementService(
    IGuildNotificationSettingsRepository notificationSettingsRepository,
    IRaidCompositionRepository raidCompositionRepository,
    IRaidEventRepository raidEventRepository,
    IRaidNotificationContentBuilder contentBuilder,
    IDiscordBotService discordBotService,
    ILogger<RaidCompositionAnnouncementService> logger) : IRaidCompositionAnnouncementService
{
    /// <inheritdoc/>
    public async Task PublishOrUpdateAnnouncementAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        // An explicit per-raid channel choice is itself the opt-in, independent of the guild-wide toggle.
        string? resolvedChannelId = raidEvent.DedicatedAnnouncementChannelId;
        if (resolvedChannelId is null)
        {
            var setting = await notificationSettingsRepository.GetAsync(
                raidEvent.GuildId, GuildNotificationEventType.RaidCompositionAnnouncementPosted, raidEvent.GuildBranchId, cancellationToken);
            if (setting is not { Enabled: true, ChannelId: not null })
                return;

            resolvedChannelId = setting.ChannelId;
        }

        try
        {
            var assignments = await raidCompositionRepository.GetAssignmentsForEventAsync(raidEvent.Id, cancellationToken);
            var embed = await contentBuilder.BuildCompositionAnnouncementAsync(raidEvent.GuildId, raidEvent, assignments, cancellationToken);
            var channelId = ulong.Parse(resolvedChannelId);

            if (raidEvent.CompositionAnnouncementChannelId is null || raidEvent.CompositionAnnouncementMessageId is null)
            {
                var messageId = await discordBotService.Messages.PostEmbedAsync(channelId, embed, cancellationToken);
                await raidEventRepository.UpdateCompositionAnnouncementReferenceAsync(
                    raidEvent.Id, raidEvent.GuildBranchId, channelId.ToString(), messageId.ToString(), cancellationToken);
            }
            else
            {
                await discordBotService.Messages.EditEmbedAsync(
                    ulong.Parse(raidEvent.CompositionAnnouncementChannelId),
                    ulong.Parse(raidEvent.CompositionAnnouncementMessageId),
                    embed, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // A failed Discord post/edit (missing permissions, deleted message/channel, bot down,
            // ...) must never fail the domain command that triggered it — same contract as
            // IGuildNotificationDispatcher. Deliberately not falling back to re-posting on an edit
            // failure: a deleted standing message likely means an officer removed it on purpose.
            logger.LogWarning(
                ex,
                "Failed to post/update composition announcement for raid event {RaidEventId} in guild {GuildId}",
                raidEvent.Id, raidEvent.GuildId);
        }
    }

    /// <inheritdoc/>
    public Task NotifyPlayerAddedAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, bool isInitialPublish, CancellationToken cancellationToken = default) =>
        NotifyPlayerAsync(raidEvent, playerDiscordId, ct => contentBuilder.BuildPlayerAddedDmAsync(raidEvent.GuildId, raidEvent, character, isInitialPublish, ct), cancellationToken);

    /// <inheritdoc/>
    public Task NotifyPlayerRemovedAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, CancellationToken cancellationToken = default) =>
        NotifyPlayerAsync(raidEvent, playerDiscordId, ct => contentBuilder.BuildPlayerRemovedDmAsync(raidEvent.GuildId, raidEvent, character, ct), cancellationToken);

    /// <inheritdoc/>
    public Task NotifyPlayerSpecChangedAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default) =>
        NotifyPlayerAsync(raidEvent, playerDiscordId, ct => contentBuilder.BuildPlayerSpecChangedDmAsync(raidEvent.GuildId, raidEvent, character, oldSpecName, newSpecName, ct), cancellationToken);

    /// <inheritdoc/>
    public async Task DeleteAnnouncementAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        if (raidEvent.CompositionAnnouncementChannelId is null || raidEvent.CompositionAnnouncementMessageId is null)
            return;

        try
        {
            await discordBotService.Messages.DeleteMessageAsync(
                ulong.Parse(raidEvent.CompositionAnnouncementChannelId),
                ulong.Parse(raidEvent.CompositionAnnouncementMessageId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Already-deleted message, missing permissions, bot down, ... — never fail the
            // caller's own command (the event delete itself already succeeded by this point).
            logger.LogWarning(
                ex,
                "Failed to delete composition announcement for raid event {RaidEventId} in guild {GuildId}",
                raidEvent.Id, raidEvent.GuildId);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyPlayerRaidCancelledAsync(RaidEvent raidEvent, string playerDiscordId, RaidCharacterRef character, CancellationToken cancellationToken = default)
    {
        // Deliberately no setting check — this DM is a guaranteed safety net, not opt-in.
        try
        {
            var embed = await contentBuilder.BuildRaidCancelledDmAsync(raidEvent.GuildId, raidEvent, character, cancellationToken);
            await discordBotService.Messages.SendDirectMessageEmbedAsync(ulong.Parse(playerDiscordId), embed, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to DM player {PlayerDiscordId} about the cancellation of raid event {RaidEventId} in guild {GuildId}",
                playerDiscordId, raidEvent.Id, raidEvent.GuildId);
        }
    }

    private async Task NotifyPlayerAsync(
        RaidEvent raidEvent,
        string playerDiscordId,
        Func<CancellationToken, Task<DiscordEmbedContent>> buildEmbed,
        CancellationToken cancellationToken)
    {
        var setting = await notificationSettingsRepository.GetAsync(
            raidEvent.GuildId, GuildNotificationEventType.RaidCompositionAnnouncementDm, raidEvent.GuildBranchId, cancellationToken);
        if (setting is not { Enabled: true })
            return;

        try
        {
            var embed = await buildEmbed(cancellationToken);
            await discordBotService.Messages.SendDirectMessageEmbedAsync(ulong.Parse(playerDiscordId), embed, cancellationToken);
        }
        catch (Exception ex)
        {
            // DMs commonly fail (closed DMs, blocked bot, left server) — never fail the caller's command.
            logger.LogWarning(
                ex,
                "Failed to DM player {PlayerDiscordId} about raid event {RaidEventId} in guild {GuildId}",
                playerDiscordId, raidEvent.Id, raidEvent.GuildId);
        }
    }
}
