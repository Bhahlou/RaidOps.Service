using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Events.Services;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.QueryHandlers;

/// <inheritdoc cref="GetRaidEventQuery"/>
public class GetRaidEventQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IRaidEventRepository raidEventRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IRaidBoardEnrichmentDataLoader enrichmentDataLoader) : IQueryHandlerAsync<GetRaidEventQuery, RaidEventResponse>
{
    /// <inheritdoc/>
    public async Task<Result<RaidEventResponse>> HandleAsync(GetRaidEventQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<RaidEventResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);
        if (guild == null)
            return Result<RaidEventResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{query.GuildId}' does not exist.");

        var raidEvent = await raidEventRepository.GetByIdAsync(query.EventId, query.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<RaidEventResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{query.EventId}' does not exist.");

        // Same draft-visibility rule as the board — see GetRaidBoardQueryHandler.
        var visibleToRequester = raidEvent.PublicationStatus == RaidPublicationStatus.Published || raidEvent.SignupMode == SignupMode.Signup;
        if (accessLevel < GuildAccessLevel.Officer && !visibleToRequester)
            return Result<RaidEventResponse>.Fail(ResponseDetail.Forbidden, "This raid event isn't published yet.");

        var rosterMemberships = await guildMembershipRepository.GetByGuildBranchIdAsync(query.GuildBranchId, cancellationToken);
        var rosterPlayerIds = rosterMemberships.Select(m => m.Character.UserDiscordId).Distinct().ToList();

        var localDate = DateOnly.FromDateTime(GuildTimeHelper.ToGuildLocalDateTime(raidEvent.StartsAtUtc, guild.Timezone));

        var enrichment = await enrichmentDataLoader.LoadAsync([raidEvent], rosterPlayerIds, query.GuildId, query.GuildBranchId, localDate, localDate, cancellationToken);

        var context = new RaidEventMappingContext(guild, rosterPlayerIds, enrichment.PlayersById, enrichment.AvailabilityLookup, enrichment.SignupsByEvent, enrichment.RaidSpecsByCharacter, query.RequesterDiscordId);

        return Result<RaidEventResponse>.Ok(RaidEventResponseMapper.Map(raidEvent, context));
    }
}
