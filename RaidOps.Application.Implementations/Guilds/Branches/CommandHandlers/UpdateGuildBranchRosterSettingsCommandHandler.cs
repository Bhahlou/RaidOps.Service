using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Branches.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateGuildBranchRosterSettingsCommand"/> by verifying the requester holds
/// Officer access on this specific branch (admin, or one of the branch's Officer roles), then
/// persisting the roster/officer role-set configuration.
/// </summary>
public class UpdateGuildBranchRosterSettingsCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IAuditLogService auditLogService,
    ILogger<UpdateGuildBranchRosterSettingsCommandHandler> logger) : ICommandHandlerAsync<UpdateGuildBranchRosterSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildBranchRosterSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var branch = await guildBranchesRepository.GetByIdAsync(command.GuildBranchId, cancellationToken);
        if (branch == null || branch.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBranchNotFound, "This guild branch does not exist.");

        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var oldRosterMode = branch.RosterMode;
        var oldRosterRoleIds = branch.RosterRoleIds;
        var oldOfficerRoleIds = branch.OfficerRoleIds;

        await guildBranchesRepository.UpdateRosterSettingsAsync(
            command.GuildBranchId,
            command.RosterMode,
            command.RosterRoleIds,
            command.OfficerRoleIds,
            cancellationToken);

        var changedFields = new List<string>();
        if (oldRosterMode != command.RosterMode)
            changedFields.Add("rosterMode");
        if (!oldRosterRoleIds.SequenceEqual(command.RosterRoleIds))
            changedFields.Add("rosterRoleIds");
        if (!oldOfficerRoleIds.SequenceEqual(command.OfficerRoleIds))
            changedFields.Add("officerRoleIds");

        if (changedFields.Count > 0)
        {
            await auditLogService.LogAsync(
                command.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.BranchRosterSettingsUpdated,
                new Dictionary<string, string>
                {
                    ["branchId"] = branch.BranchId.ToString(),
                    ["changedFields"] = string.Join(',', changedFields),
                },
                cancellationToken);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Guild {GuildId} branch {GuildBranchId} roster settings updated by discord user {DiscordId}: fields [{ChangedFields}]",
                command.GuildId, command.GuildBranchId, command.RequesterDiscordId, string.Join(", ", changedFields));
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Branch roster settings updated successfully."));
    }
}
