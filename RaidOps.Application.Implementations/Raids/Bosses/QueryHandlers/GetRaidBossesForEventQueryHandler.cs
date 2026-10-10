using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Bosses.Queries;
using RaidOps.Application.Contracts.Raids.Bosses.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Bosses.QueryHandlers;

/// <summary>
/// Handles <see cref="GetRaidBossesForEventQuery"/> by returning every boss of the zone(s) the raid
/// event targets — same visibility rule as the event's attributions themselves (roster, gated by
/// publication status).
/// </summary>
public class GetRaidBossesForEventQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidBossRepository raidBossRepository) : IQueryHandlerAsync<GetRaidBossesForEventQuery, List<RaidBossResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidBossResponse>>> HandleAsync(GetRaidBossesForEventQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidBossResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var raidEvent = await raidEventRepository.GetByIdAsync(query.EventId, query.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<List<RaidBossResponse>>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{query.EventId}' does not exist.");

        var visibleToRequester = raidEvent.PublicationStatus == RaidPublicationStatus.Published || raidEvent.SignupMode == SignupMode.Signup;
        if (accessLevel < GuildAccessLevel.Officer && !visibleToRequester)
            return Result<List<RaidBossResponse>>.Fail(ResponseDetail.Forbidden, "This raid event isn't published yet.");

        var zoneIds = raidEvent.TargetZones.Select(z => z.RaidZoneId);
        var bosses = await raidBossRepository.GetForZonesAsync(zoneIds, cancellationToken);

        return Result<List<RaidBossResponse>>.Ok([.. bosses.Select(GetRaidBossesForZoneQueryHandler.MapBoss)]);
    }
}
