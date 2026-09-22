using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.CommandHandlers;

/// <summary>Handles <see cref="ReorderRaidPlanPagesCommand"/> by validating the officer's access and the target board, then re-numbering the pages.</summary>
public class ReorderRaidPlanPagesCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository,
    IRaidPlanPagesRepository pagesRepository) : ICommandHandlerAsync<ReorderRaidPlanPagesCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(ReorderRaidPlanPagesCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var plan = await plansRepository.GetByIdAsync(command.RaidPlanId, cancellationToken);
        if (plan == null || plan.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanNotFound, $"Raid plan '{command.RaidPlanId}' does not exist on this guild.");

        await pagesRepository.ReorderAsync(command.RaidPlanId, command.OrderedIds, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid plan pages reordered successfully."));
    }
}
