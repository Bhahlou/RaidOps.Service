using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.QueryHandlers;

/// <inheritdoc cref="GetRaidEventSummaryQuery"/>
public class GetRaidEventSummaryQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidSignupRepository raidSignupRepository) : IQueryHandlerAsync<GetRaidEventSummaryQuery, RaidEventSummaryResponse>
{
    /// <inheritdoc/>
    public async Task<Result<RaidEventSummaryResponse>> HandleAsync(GetRaidEventSummaryQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<RaidEventSummaryResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var raidEvent = await raidEventRepository.GetByIdAsync(query.EventId, query.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<RaidEventSummaryResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{query.EventId}' does not exist.");

        RaidSignup? mySignup = null;
        if (raidEvent.SignupMode == SignupMode.Signup)
            mySignup = await raidSignupRepository.GetAsync(raidEvent.Id, query.RequesterDiscordId, cancellationToken);

        return Result<RaidEventSummaryResponse>.Ok(new RaidEventSummaryResponse
        {
            Id = raidEvent.Id,
            Name = raidEvent.Name,
            SignupMode = raidEvent.SignupMode,
            MySignupStatus = mySignup?.Status,
            MySignupCharacterId = mySignup?.CharacterId,
            MySignupSpecId = mySignup?.SpecId,
        });
    }
}
