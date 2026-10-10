using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Bosses.Queries;
using RaidOps.Application.Contracts.Raids.Bosses.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Bosses.QueryHandlers;

/// <summary>Handles <see cref="GetRaidBossesForZoneQuery"/> by returning every boss seeded for the given zone.</summary>
public class GetRaidBossesForZoneQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidBossRepository raidBossRepository) : IQueryHandlerAsync<GetRaidBossesForZoneQuery, List<RaidBossResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidBossResponse>>> HandleAsync(GetRaidBossesForZoneQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<RaidBossResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var bosses = await raidBossRepository.GetForZonesAsync([query.RaidZoneId], cancellationToken);
        return Result<List<RaidBossResponse>>.Ok([.. bosses.Select(MapBoss)]);
    }

    internal static RaidBossResponse MapBoss(RaidBoss boss) => new()
    {
        Id = boss.Id,
        Name = boss.Name,
        IconUrl = boss.IconUrl,
        SortOrder = boss.SortOrder,
        RaidZoneId = boss.RaidZoneId,
        RaidZoneName = boss.RaidZone.Name,
        RaidZoneShortCode = boss.RaidZone.ShortCode,
    };
}
