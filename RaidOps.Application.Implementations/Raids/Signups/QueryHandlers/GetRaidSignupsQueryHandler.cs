using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Signups.QueryHandlers;

/// <inheritdoc cref="GetRaidSignupsQuery"/>
public class GetRaidSignupsQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidSignupResponseBuilder raidSignupResponseBuilder) : IQueryHandlerAsync<GetRaidSignupsQuery, List<RaidSignupResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidSignupResponse>>> HandleAsync(GetRaidSignupsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidSignupResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var raidEvent = await raidEventRepository.GetByIdAsync(query.EventId, query.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<List<RaidSignupResponse>>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{query.EventId}' does not exist.");

        var responses = await raidSignupResponseBuilder.BuildAsync(raidEvent, cancellationToken);
        var sorted = responses.OrderBy(r => r.PlayerName, StringComparer.OrdinalIgnoreCase).ToList();

        return Result<List<RaidSignupResponse>>.Ok(sorted);
    }
}
