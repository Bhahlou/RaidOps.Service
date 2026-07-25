using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Branches.CommandHandlers;

/// <summary>
/// Handles <see cref="ActivateGuildBranchCommand"/> by verifying admin rights, confirming the
/// guild is registered, then activating the branch (creating it, or reactivating a previously
/// deactivated one).
/// </summary>
public class ActivateGuildBranchCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IAuditLogService auditLogService,
    ILogger<ActivateGuildBranchCommandHandler> logger) : ICommandHandlerAsync<ActivateGuildBranchCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(ActivateGuildBranchCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        var existing = await guildBranchesRepository.GetByGuildAndBranchAsync(command.GuildId, command.BranchId, cancellationToken);
        if (existing?.IsActive == true)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBranchAlreadyActive, "This branch is already active on the guild.");

        await guildBranchesRepository.ActivateAsync(command.GuildId, command.BranchId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.BranchActivated,
            new Dictionary<string, string> { ["branchId"] = command.BranchId.ToString() },
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Guild {GuildId} activated branch {BranchId}, requested by discord user {DiscordId}",
                command.GuildId, command.BranchId, command.RequesterDiscordId);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Branch activated successfully."));
    }
}
