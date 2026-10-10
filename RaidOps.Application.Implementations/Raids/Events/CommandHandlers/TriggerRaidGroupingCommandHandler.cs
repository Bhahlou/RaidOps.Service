using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.CommandHandlers;

/// <summary>
/// Handles <see cref="TriggerRaidGroupingCommand"/> — verifies officer access, resolves the
/// branch's composition-announcement channel, resolves which assigned character the ping should
/// reference (the requester's own, or an explicit <see cref="TriggerRaidGroupingCommand.CharacterName"/>),
/// and posts a one-off "whisper this character for an invite" message with the current composition
/// attached as an embed (a snapshot — unlike the standing announcement, this one is never edited
/// afterward, so it can drift from the roster if it changes later). Unlike the rest of the
/// composition-announcement family, this is a synchronous, user-facing action: a missing channel,
/// empty roster, or unresolved character is returned as a real failure (not silently swallowed),
/// since an officer explicitly triggered it and needs to know why nothing was sent.
/// </summary>
public class TriggerRaidGroupingCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidCompositionRepository raidCompositionRepository,
    IGuildNotificationSettingsRepository notificationSettingsRepository,
    IRaidNotificationContentBuilder contentBuilder,
    IDiscordBotService discordBotService) : ICommandHandlerAsync<TriggerRaidGroupingCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(TriggerRaidGroupingCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(command.EventId, command.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{command.EventId}' does not exist.");

        if (raidEvent.PublicationStatus != RaidPublicationStatus.Published)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidEventNotPublished, "Only a published raid event can trigger a grouping ping.");

        string? resolvedChannelId = raidEvent.DedicatedAnnouncementChannelId;
        if (resolvedChannelId is null)
        {
            var setting = await notificationSettingsRepository.GetAsync(
                command.GuildId, GuildNotificationEventType.RaidCompositionAnnouncementPosted, command.GuildBranchId, cancellationToken);
            if (setting is not { Enabled: true, ChannelId: not null })
                return Result<CommandResponse>.Fail(ResponseDetail.NoAnnouncementChannelConfigured, "No composition announcement channel is configured for this branch.");

            resolvedChannelId = setting.ChannelId;
        }

        var assignments = await raidCompositionRepository.GetAssignmentsForEventAsync(command.EventId, cancellationToken);
        if (assignments.Count == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.NoAssignmentsToNotify, "This raid has no assigned players to ping.");

        string groupingCharacterName;
        if (!string.IsNullOrWhiteSpace(command.CharacterName))
        {
            var match = assignments.FirstOrDefault(a => string.Equals(a.Character.Name, command.CharacterName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                return Result<CommandResponse>.Fail(ResponseDetail.RaidGroupingCharacterNotFound, $"No character named '{command.CharacterName}' is assigned to this raid.");
            groupingCharacterName = match.Character.Name;
        }
        else
        {
            var own = assignments.FirstOrDefault(a => a.AssignedPlayerDiscordId == command.RequesterDiscordId);
            if (own == null)
                return Result<CommandResponse>.Fail(ResponseDetail.RaidGroupingRequesterHasNoCharacter, "You have no character assigned to this raid — specify which character the ping should reference.");
            groupingCharacterName = own.Character.Name;
        }

        var language = await contentBuilder.GetGuildLanguageAsync(command.GuildId, cancellationToken);
        var mentions = string.Join(' ', assignments.Select(a => $"<@{a.AssignedPlayerDiscordId}>").Distinct());
        var message = RaidNotificationText.GetGroupingPingMessage(mentions, raidEvent.Name, groupingCharacterName, language);
        var embed = await contentBuilder.BuildCompositionAnnouncementAsync(command.GuildId, raidEvent, assignments, cancellationToken);

        await discordBotService.Messages.SendMessageWithEmbedAsync(ulong.Parse(resolvedChannelId), message, embed, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Grouping ping sent."));
    }
}
