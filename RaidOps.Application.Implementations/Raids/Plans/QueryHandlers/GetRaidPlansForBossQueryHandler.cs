using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Queries;
using RaidOps.Application.Contracts.Raids.Plans.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.QueryHandlers;

/// <summary>Handles <see cref="GetRaidPlansForBossQuery"/> by returning every strategy board the guild has for the given boss.</summary>
public class GetRaidPlansForBossQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository) : IQueryHandlerAsync<GetRaidPlansForBossQuery, List<RaidPlanResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidPlanResponse>>> HandleAsync(GetRaidPlansForBossQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidPlanResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var plans = await plansRepository.GetForBossAsync(query.GuildId, query.RaidBossId, cancellationToken);

        var response = plans.Select(p => new RaidPlanResponse { Id = p.Id, Name = p.Name, RaidBossId = p.RaidBossId }).ToList();

        return Result<List<RaidPlanResponse>>.Ok(response);
    }
}
