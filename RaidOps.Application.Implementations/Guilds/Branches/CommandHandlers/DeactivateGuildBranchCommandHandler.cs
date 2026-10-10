using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Branches.CommandHandlers;

/// <summary>
/// Handles <see cref="DeactivateGuildBranchCommand"/> by verifying admin rights, then deactivating
/// the branch (never hard-deletes — roster history and role-set configuration are preserved).
/// </summary>
public class DeactivateGuildBranchCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IBranchRepository branchRepository,
    IAuditLogService auditLogService,
    ILogger<DeactivateGuildBranchCommandHandler> logger) : ICommandHandlerAsync<DeactivateGuildBranchCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeactivateGuildBranchCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var branch = await guildBranchesRepository.GetByIdAsync(command.GuildBranchId, cancellationToken);
        if (branch == null || branch.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBranchNotFound, "This guild branch does not exist.");

        await guildBranchesRepository.DeactivateAsync(command.GuildBranchId, cancellationToken);

        var wowBranch = await branchRepository.GetByIdAsync(branch.BranchId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.BranchDeactivated,
            new Dictionary<string, string>
            {
                ["branchId"] = branch.BranchId.ToString(),
                ["branchName"] = wowBranch?.Name ?? "Unknown",
            },
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Guild {GuildId} deactivated branch {GuildBranchId}, requested by discord user {DiscordId}",
                command.GuildId, command.GuildBranchId, command.RequesterDiscordId);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Branch deactivated successfully."));
    }
}
