using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateGuildNotificationSettingsCommand"/> by verifying admin rights,
/// confirming the guild is registered, diffing against the previously effective settings for this
/// scope, then upserting every row in a single pass.
/// </summary>
public class UpdateGuildNotificationSettingsCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IGuildNotificationSettingsRepository notificationSettingsRepository,
    IDiscordBotService discordBotService,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateGuildNotificationSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildNotificationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = command.GuildBranchId != null
            ? await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId.Value, cancellationToken)
            : await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild/branch.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        // Captured before UpsertRangeAsync so the audit log can report what actually moved,
        // scoped to what this branch/guild-wide view was showing the officer before the edit.
        var previous = await notificationSettingsRepository.GetEffectiveForGuildAsync(command.GuildId, command.GuildBranchId, cancellationToken);
        var previousByEventType = previous.ToDictionary(s => s.EventType);

        var settings = command.Settings.Select(s => new GuildNotificationSetting
        {
            GuildId = command.GuildId,
            GuildBranchId = command.GuildBranchId,
            EventType = s.EventType,
            Enabled = s.Enabled,
            ChannelId = s.Enabled ? s.ChannelId : null,
        }).ToList();

        await notificationSettingsRepository.UpsertRangeAsync(command.GuildId, command.GuildBranchId, settings, cancellationToken);

        var (variables, changedEvents) = BuildChangeVariables(command.GuildId, previousByEventType, settings, cancellationToken);

        if (changedEvents.Count > 0)
        {
            variables["changedEvents"] = string.Join(',', changedEvents);
            await auditLogService.LogAsync(
                command.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.NotificationSettingsUpdated,
                variables,
                cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Notification settings updated successfully."));
    }

    /// <summary>
    /// Diffs each submitted row against the effective row it replaces, recording only the event
    /// types whose enabled state or channel actually changed. Channel IDs are resolved to names
    /// via the bot's Gateway cache (one fetch for the whole batch) so the audit log stays readable
    /// even after a channel gets renamed later.
    /// </summary>
    private (Dictionary<string, string> Variables, List<string> ChangedEvents) BuildChangeVariables(
        string guildId,
        Dictionary<GuildNotificationEventType, GuildNotificationSetting> previousByEventType,
        List<GuildNotificationSetting> newSettings,
        CancellationToken cancellationToken)
    {
        var channelNamesById = ResolveChannelNames(guildId, cancellationToken);
        var variables = new Dictionary<string, string>();
        var changedEvents = new List<string>();

        foreach (var setting in newSettings)
        {
            previousByEventType.TryGetValue(setting.EventType, out var previous);
            var oldEnabled = previous?.Enabled ?? false;
            var oldChannelId = previous?.ChannelId;

            if (oldEnabled == setting.Enabled && oldChannelId == setting.ChannelId)
                continue;

            var eventName = setting.EventType.ToString();
            changedEvents.Add(eventName);
            variables[$"old{eventName}Enabled"] = oldEnabled ? "true" : "false";
            variables[$"new{eventName}Enabled"] = setting.Enabled ? "true" : "false";

            if (oldEnabled && oldChannelId is not null)
                variables[$"old{eventName}ChannelName"] = channelNamesById.GetValueOrDefault(oldChannelId, oldChannelId);

            if (setting.Enabled && setting.ChannelId is not null)
                variables[$"new{eventName}ChannelName"] = channelNamesById.GetValueOrDefault(setting.ChannelId, setting.ChannelId);
        }

        return (variables, changedEvents);
    }

    private Dictionary<string, string> ResolveChannelNames(string guildId, CancellationToken cancellationToken)
    {
        try
        {
            return discordBotService.Guilds.GetChannels(guildId, cancellationToken)
                .ToDictionary(c => c.ChannelId.ToString(), c => c.Name);
        }
        catch (InvalidOperationException)
        {
            // Bot isn't in the guild's cache (e.g. removed after the settings were configured) —
            // fall back to raw channel IDs rather than failing the whole update.
            return [];
        }
    }
}
