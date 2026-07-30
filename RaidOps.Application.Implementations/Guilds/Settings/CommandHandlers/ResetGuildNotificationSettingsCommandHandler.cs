using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
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
    IAuditLogService auditLogService) : ICommandHandlerAsync<ResetGuildNotificationSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(ResetGuildNotificationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this branch.");

        await notificationSettingsRepository.DeleteAsync(command.GuildId, command.GuildBranchId, command.EventType, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.NotificationSettingsReset,
            new Dictionary<string, string>
            {
                ["guildBranchId"] = command.GuildBranchId.ToString(),
                ["eventType"] = command.EventType.ToString(),
            },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Notification setting reset to the guild-wide fallback."));
    }
}
