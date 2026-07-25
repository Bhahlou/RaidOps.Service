using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateGuildNotificationSettingsCommand"/> by verifying admin rights,
/// confirming the guild is registered, then upserting every row in a single pass.
/// </summary>
public class UpdateGuildNotificationSettingsCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IGuildNotificationSettingsRepository notificationSettingsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateGuildNotificationSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildNotificationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        var settings = command.Settings.Select(s => new GuildNotificationSetting
        {
            GuildId = command.GuildId,
            EventType = s.EventType,
            Enabled = s.Enabled,
            ChannelId = s.Enabled ? s.ChannelId : null,
        });

        await notificationSettingsRepository.UpsertRangeAsync(command.GuildId, settings, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.NotificationSettingsUpdated,
            new Dictionary<string, string> { ["eventCount"] = command.Settings.Count.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Notification settings updated successfully."));
    }
}
