using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Zones.Queries;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Zones.QueryHandlers;

/// <summary>
/// Handles <see cref="GetRaidZonesForBranchQuery"/> by resolving the branch's currently active
/// expansion and returning every raid zone seeded for it.
/// </summary>
public class GetRaidZonesForBranchQueryHandler(
    IGuildAccessService guildAccessService,
    IBranchRepository branchRepository,
    IRaidZoneRepository raidZoneRepository) : IQueryHandlerAsync<GetRaidZonesForBranchQuery, List<RaidZoneResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidZoneResponse>>> HandleAsync(GetRaidZonesForBranchQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidZoneResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild's roster.");

        var branch = await branchRepository.GetByIdAsync(query.BranchId, cancellationToken);
        if (branch == null)
            return Result<List<RaidZoneResponse>>.Fail(ResponseDetail.BranchNotFound, $"Branch '{query.BranchId}' does not exist.");

        var zones = await raidZoneRepository.GetByExpansionIdAsync(branch.CurrentExpansionId, cancellationToken);

        return Result<List<RaidZoneResponse>>.Ok([.. zones.Select(MapZone)]);
    }

    private static RaidZoneResponse MapZone(RaidZone zone) => new()
    {
        Id = zone.Id,
        Name = zone.Name,
        ShortCode = zone.ShortCode,
        GroupCount = zone.GroupCount,
        SlotsPerGroup = zone.SlotsPerGroup,
        IconUrl = zone.IconUrl,
        SortOrder = zone.SortOrder,
    };
}
