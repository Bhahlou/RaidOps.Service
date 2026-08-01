using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Branches.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Branches.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateGuildBranchRegionCommand"/> by verifying the requester holds Officer
/// access on this specific branch (admin, or one of the branch's Officer roles), then persisting
/// its Blizzard API region.
/// </summary>
public class UpdateGuildBranchRegionCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IBranchRepository branchRepository,
    IAuditLogService auditLogService,
    ILogger<UpdateGuildBranchRegionCommandHandler> logger) : ICommandHandlerAsync<UpdateGuildBranchRegionCommand>
{
    private static readonly string[] ValidRegions = ["eu", "us", "kr", "tw"];

    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildBranchRegionCommand command, CancellationToken cancellationToken = default)
    {
        if (!ValidRegions.Contains(command.Region))
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidRegion, $"'{command.Region}' is not a recognized region.");

        var branch = await guildBranchesRepository.GetByIdAsync(command.GuildBranchId, cancellationToken);
        if (branch == null || branch.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBranchNotFound, "This guild branch does not exist.");

        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var oldRegion = branch.Region;
        if (oldRegion == command.Region)
            return Result<CommandResponse>.Ok(new CommandResponse("Branch region unchanged."));

        await guildBranchesRepository.UpdateRegionAsync(command.GuildBranchId, command.Region, cancellationToken);

        var wowBranch = await branchRepository.GetByIdAsync(branch.BranchId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.BranchRegionUpdated,
            new Dictionary<string, string>
            {
                ["branchId"] = branch.BranchId.ToString(),
                ["branchName"] = wowBranch?.Name ?? "Unknown",
                ["oldRegion"] = oldRegion ?? "(none)",
                ["newRegion"] = command.Region,
            },
            cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Guild {GuildId} branch {GuildBranchId} region updated to {Region} by discord user {DiscordId}",
                command.GuildId, command.GuildBranchId, command.Region, command.RequesterDiscordId);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Branch region updated successfully."));
    }
}
