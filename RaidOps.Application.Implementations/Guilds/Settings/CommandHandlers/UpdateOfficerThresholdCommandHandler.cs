using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Guilds.Settings.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateOfficerThresholdCommand"/> by verifying admin rights, confirming the
/// guild is registered, then persisting the Officer role threshold. Mirrors
/// <see cref="UpdateGuildSettingsCommandHandler"/>'s handling of the roster role threshold —
/// no strict validation against the bot's role list, best-effort audit log resolution only.
/// </summary>
public class UpdateOfficerThresholdCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService,
    IAuditLogService auditLogService,
    ILogger<UpdateOfficerThresholdCommandHandler> logger) : ICommandHandlerAsync<UpdateOfficerThresholdCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateOfficerThresholdCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        var oldMinOfficerRoleId = guild.MinOfficerRoleId;

        await guildsRepository.UpdateOfficerThresholdAsync(command.GuildId, command.MinOfficerRoleId, cancellationToken);

        if (oldMinOfficerRoleId != command.MinOfficerRoleId)
        {
            var roles = RoleChangeAuditHelper.TryGetRoles(discordBotService, command.GuildId, cancellationToken);
            var variables = new Dictionary<string, string> { ["changedFields"] = "minOfficerRoleId" };
            if (oldMinOfficerRoleId != null)
                RoleChangeAuditHelper.AddRoleVariables(variables, "old", "MinOfficerRole", roles, oldMinOfficerRoleId);
            RoleChangeAuditHelper.AddRoleVariables(variables, "new", "MinOfficerRole", roles, command.MinOfficerRoleId);

            await auditLogService.LogAsync(
                command.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.OfficerThresholdUpdated,
                variables,
                cancellationToken);
        }

        logger.LogInformation(
            "Guild {GuildId} officer threshold updated by discord user {DiscordId}: {OldRoleId} -> {NewRoleId}",
            command.GuildId, command.RequesterDiscordId, oldMinOfficerRoleId, command.MinOfficerRoleId);

        return Result<CommandResponse>.Ok(new CommandResponse("Officer threshold updated successfully."));
    }
}
