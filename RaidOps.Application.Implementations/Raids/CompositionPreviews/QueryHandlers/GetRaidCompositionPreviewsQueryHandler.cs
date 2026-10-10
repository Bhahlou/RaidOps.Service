using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.CompositionPreviews.QueryHandlers;

/// <summary>Handles <see cref="GetRaidCompositionPreviewsQuery"/> by returning every preview of a guild branch, most recently updated first.</summary>
public class GetRaidCompositionPreviewsQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionPreviewsRepository raidCompositionPreviewsRepository) : IQueryHandlerAsync<GetRaidCompositionPreviewsQuery, List<RaidCompositionPreviewSummaryResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidCompositionPreviewSummaryResponse>>> HandleAsync(GetRaidCompositionPreviewsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<RaidCompositionPreviewSummaryResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var previews = await raidCompositionPreviewsRepository.GetForGuildBranchAsync(query.GuildBranchId, cancellationToken);

        return Result<List<RaidCompositionPreviewSummaryResponse>>.Ok([.. previews.Select(MapSummary)]);
    }

    private static RaidCompositionPreviewSummaryResponse MapSummary(RaidCompositionPreview preview) => new()
    {
        Id = preview.Id,
        Name = preview.Name,
        GroupCount = preview.GroupCount,
        UpdatedAt = preview.UpdatedAt ?? preview.CreatedAt,
    };
}
