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
/// Handles <see cref="GetRaidZonesForGuildQuery"/> by resolving every active branch's underlying WoW
/// branch's currently active expansion and returning the deduplicated union of raid zones seeded
/// across all of them.
/// </summary>
public class GetRaidZonesForGuildQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IBranchRepository branchRepository,
    IRaidZoneRepository raidZoneRepository) : IQueryHandlerAsync<GetRaidZonesForGuildQuery, List<RaidZoneResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidZoneResponse>>> HandleAsync(GetRaidZonesForGuildQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<RaidZoneResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var activeBranches = await guildBranchesRepository.GetActiveForGuildAsync(query.GuildId, cancellationToken);

        var expansionIds = new HashSet<int>();
        foreach (var guildBranch in activeBranches)
        {
            var branch = await branchRepository.GetByIdAsync(guildBranch.BranchId, cancellationToken);
            if (branch != null)
                expansionIds.Add(branch.CurrentExpansionId);
        }

        var zonesById = new Dictionary<int, RaidZone>();
        foreach (var expansionId in expansionIds)
        {
            foreach (var zone in await raidZoneRepository.GetByExpansionIdAsync(expansionId, cancellationToken))
                zonesById[zone.Id] = zone;
        }

        var zones = zonesById.Values.OrderBy(z => z.SortOrder).Select(MapZone).ToList();
        return Result<List<RaidZoneResponse>>.Ok(zones);
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
