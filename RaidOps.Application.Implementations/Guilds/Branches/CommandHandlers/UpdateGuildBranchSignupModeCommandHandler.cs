using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Branches.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateGuildBranchSignupModeCommand"/> by verifying the requester holds Officer
/// access on this specific branch (admin, or one of the branch's Officer roles), then persisting its
/// default raid signup mode.
/// </summary>
public class UpdateGuildBranchSignupModeCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IBranchRepository branchRepository,
    IAuditLogService auditLogService,
    ILogger<UpdateGuildBranchSignupModeCommandHandler> logger) : ICommandHandlerAsync<UpdateGuildBranchSignupModeCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildBranchSignupModeCommand command, CancellationToken cancellationToken = default)
    {
        var branch = await guildBranchesRepository.GetByIdAsync(command.GuildBranchId, cancellationToken);
        if (branch == null || branch.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBranchNotFound, "This guild branch does not exist.");

        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var oldSignupMode = branch.SignupMode;
        if (oldSignupMode == command.SignupMode)
            return Result<CommandResponse>.Ok(new CommandResponse("Branch signup mode unchanged."));

        await guildBranchesRepository.UpdateSignupModeAsync(command.GuildBranchId, command.SignupMode, cancellationToken);

        var wowBranch = await branchRepository.GetByIdAsync(branch.BranchId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.BranchSignupModeUpdated,
            new Dictionary<string, string>
            {
                ["branchId"] = branch.BranchId.ToString(),
                ["branchName"] = wowBranch?.Name ?? "Unknown",
                ["oldSignupMode"] = oldSignupMode?.ToString() ?? "(none)",
                ["newSignupMode"] = command.SignupMode.ToString(),
            },
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Guild {GuildId} branch {GuildBranchId} signup mode updated to {SignupMode} by discord user {DiscordId}",
                command.GuildId, command.GuildBranchId, command.SignupMode, command.RequesterDiscordId);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Branch signup mode updated successfully."));
    }
}
