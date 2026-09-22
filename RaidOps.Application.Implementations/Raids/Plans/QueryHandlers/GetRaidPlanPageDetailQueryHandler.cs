using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Queries;
using RaidOps.Application.Contracts.Raids.Plans.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Plans.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.QueryHandlers;

/// <summary>Handles <see cref="GetRaidPlanPageDetailQuery"/> by returning one page including its elements — the canvas editor's main load query.</summary>
public class GetRaidPlanPageDetailQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidPlanPagesRepository pagesRepository) : IQueryHandlerAsync<GetRaidPlanPageDetailQuery, RaidPlanPageDetailResponse>
{
    /// <inheritdoc/>
    public async Task<Result<RaidPlanPageDetailResponse>> HandleAsync(GetRaidPlanPageDetailQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<RaidPlanPageDetailResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var page = await pagesRepository.GetByIdAsync(query.RaidPlanPageId, cancellationToken);
        if (page == null || page.RaidPlanId != query.RaidPlanId || page.RaidPlan.GuildId != query.GuildId)
            return Result<RaidPlanPageDetailResponse>.Fail(ResponseDetail.RaidPlanPageNotFound, $"Page '{query.RaidPlanPageId}' does not exist on this plan.");

        var response = new RaidPlanPageDetailResponse
        {
            Id = page.Id,
            Name = page.Name,
            BackgroundImageKey = page.BackgroundImageKey,
            Elements = page.Elements.Select(RaidPlanElementMapper.ToResponse).ToList(),
        };

        return Result<RaidPlanPageDetailResponse>.Ok(response);
    }
}
