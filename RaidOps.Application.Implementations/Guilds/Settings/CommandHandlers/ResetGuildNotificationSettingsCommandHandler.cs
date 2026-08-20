using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;

/// <summary>
/// Handles <see cref="ResetGuildNotificationSettingsCommand"/> by verifying officer rights on the
/// target branch, then deleting the branch's override row for one event type so it falls back to
/// the guild-wide setting.
/// </summary>
public class ResetGuildNotificationSettingsCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildNotificationSettingsRepository notificationSettingsRepository,
    IDiscordBotService discordBotService,
    IAuditLogService auditLogService) : ICommandHandlerAsync<ResetGuildNotificationSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(ResetGuildNotificationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this branch.");

        // Captured before DeleteAsync: the guild-wide row is the effective value this scope falls
        // back to once the override is gone, so the audit log can show a real before/after instead
        // of just naming the event type that got reset.
        var all = await notificationSettingsRepository.GetAllForGuildAsync(command.GuildId, cancellationToken);
        var overrideRow = all.FirstOrDefault(s => s.GuildBranchId == command.GuildBranchId && s.EventType == command.EventType);
        var guildWideRow = all.FirstOrDefault(s => s.GuildBranchId == null && s.EventType == command.EventType);

        await notificationSettingsRepository.DeleteAsync(command.GuildId, command.GuildBranchId, command.EventType, cancellationToken);

        // Nothing was actually overridden for this branch/event — DeleteAsync was a no-op, nothing
        // meaningful to report.
        if (overrideRow is null)
            return Result<CommandResponse>.Ok(new CommandResponse("Notification setting reset to the guild-wide fallback."));

        var eventName = command.EventType.ToString();
        var channelNamesById = ResolveChannelNames(command.GuildId, cancellationToken);

        var variables = new Dictionary<string, string>
        {
            ["guildBranchId"] = command.GuildBranchId.ToString(),
            ["eventType"] = eventName,
            ["changedEvents"] = eventName,
            [$"old{eventName}Enabled"] = overrideRow.Enabled ? "true" : "false",
            [$"new{eventName}Enabled"] = guildWideRow?.Enabled == true ? "true" : "false",
        };

        if (overrideRow.Enabled && overrideRow.ChannelId is not null)
            variables[$"old{eventName}ChannelName"] = channelNamesById.GetValueOrDefault(overrideRow.ChannelId, overrideRow.ChannelId);

        if (guildWideRow is { Enabled: true, ChannelId: not null })
            variables[$"new{eventName}ChannelName"] = channelNamesById.GetValueOrDefault(guildWideRow.ChannelId, guildWideRow.ChannelId);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.NotificationSettingsReset,
            variables,
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Notification setting reset to the guild-wide fallback."));
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
            // Bot isn't in the guild's cache — fall back to raw channel IDs rather than failing
            // the whole reset.
            return [];
        }
    }
}
