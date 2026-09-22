using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.CommandHandlers;

/// <summary>Handles <see cref="SetRaidPlanPageBackgroundCommand"/> by validating the officer's access and the target board, then setting the page's background image key.</summary>
public class SetRaidPlanPageBackgroundCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository,
    IRaidPlanPagesRepository pagesRepository) : ICommandHandlerAsync<SetRaidPlanPageBackgroundCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(SetRaidPlanPageBackgroundCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var plan = await plansRepository.GetByIdAsync(command.RaidPlanId, cancellationToken);
        if (plan == null || plan.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanNotFound, $"Raid plan '{command.RaidPlanId}' does not exist on this guild.");

        var updated = await pagesRepository.SetBackgroundAsync(command.RaidPlanPageId, command.RaidPlanId, command.BackgroundImageKey, cancellationToken);
        if (!updated)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanPageNotFound, $"Page '{command.RaidPlanPageId}' does not exist on this plan.");

        return Result<CommandResponse>.Ok(new CommandResponse("Raid plan page background updated successfully."));
    }
}
