using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateGuildSettingsCommand"/> by verifying admin rights,
/// confirming the guild is registered, then persisting the settings.
/// </summary>
public class UpdateGuildSettingsCommandHandler(
    IUserGuildsRepository userGuildsRepository,
    IGuildsRepository guildsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateGuildSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(command.RequesterDiscordId, cancellationToken);
        var membership = userGuilds.FirstOrDefault(g => g.GuildId == command.GuildId);

        if (membership == null || !membership.IsAdmin)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        await guildsRepository.UpdateSettingsAsync(
            command.GuildId,
            command.Timezone,
            command.RosterMode,
            command.MinRosterRoleId,
            cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.SettingsUpdated,
            cancellationToken: cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Guild settings updated successfully."));
    }
}
