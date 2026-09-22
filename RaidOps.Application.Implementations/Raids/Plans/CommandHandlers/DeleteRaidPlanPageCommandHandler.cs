using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.CommandHandlers;

/// <summary>Handles <see cref="DeleteRaidPlanPageCommand"/> by validating the officer's access, then permanently removing the page and its elements.</summary>
public class DeleteRaidPlanPageCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository,
    IRaidPlanPagesRepository pagesRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeleteRaidPlanPageCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteRaidPlanPageCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var plan = await plansRepository.GetByIdAsync(command.RaidPlanId, cancellationToken);
        if (plan == null || plan.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanNotFound, $"Raid plan '{command.RaidPlanId}' does not exist on this guild.");

        var deleted = await pagesRepository.DeleteAsync(command.RaidPlanPageId, command.RaidPlanId, cancellationToken);
        if (!deleted)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanPageNotFound, $"Page '{command.RaidPlanPageId}' does not exist on this plan.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidPlanUpdated,
            new Dictionary<string, string> { ["raidPlanPageId"] = command.RaidPlanPageId.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid plan page deleted successfully."));
    }
}
