using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Queries;
using RaidOps.Application.Contracts.Raids.Plans.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.QueryHandlers;

/// <summary>Handles <see cref="GetRaidPlanPagesQuery"/> by returning a board's page-tab list.</summary>
public class GetRaidPlanPagesQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository,
    IRaidPlanPagesRepository pagesRepository) : IQueryHandlerAsync<GetRaidPlanPagesQuery, List<RaidPlanPageResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidPlanPageResponse>>> HandleAsync(GetRaidPlanPagesQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidPlanPageResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var plan = await plansRepository.GetByIdAsync(query.RaidPlanId, cancellationToken);
        if (plan == null || plan.GuildId != query.GuildId)
            return Result<List<RaidPlanPageResponse>>.Fail(ResponseDetail.RaidPlanNotFound, $"Raid plan '{query.RaidPlanId}' does not exist on this guild.");

        var pages = await pagesRepository.GetForPlanAsync(query.RaidPlanId, cancellationToken);

        var response = pages.Select(p => new RaidPlanPageResponse
        {
            Id = p.Id,
            Name = p.Name,
            SortOrder = p.SortOrder,
            BackgroundImageKey = p.BackgroundImageKey,
        }).ToList();

        return Result<List<RaidPlanPageResponse>>.Ok(response);
    }
}
