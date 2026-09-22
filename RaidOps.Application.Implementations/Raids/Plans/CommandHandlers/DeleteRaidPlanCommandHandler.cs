using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.CommandHandlers;

/// <summary>Handles <see cref="DeleteRaidPlanCommand"/> by validating the officer's access, then permanently removing the board and its pages/elements.</summary>
public class DeleteRaidPlanCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeleteRaidPlanCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteRaidPlanCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var deleted = await plansRepository.DeleteAsync(command.RaidPlanId, command.GuildId, cancellationToken);
        if (!deleted)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanNotFound, $"Raid plan '{command.RaidPlanId}' does not exist on this guild.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidPlanUpdated,
            new Dictionary<string, string> { ["raidPlanId"] = command.RaidPlanId.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid plan deleted successfully."));
    }
}
