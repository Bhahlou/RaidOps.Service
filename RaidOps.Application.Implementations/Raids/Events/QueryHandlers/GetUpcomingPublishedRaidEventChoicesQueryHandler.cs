using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.QueryHandlers;

/// <inheritdoc cref="GetUpcomingPublishedRaidEventChoicesQuery"/>
public class GetUpcomingPublishedRaidEventChoicesQueryHandler(
    IRaidEventRepository raidEventRepository,
    IGuildsRepository guildsRepository) : IQueryHandlerAsync<GetUpcomingPublishedRaidEventChoicesQuery, List<RaidEventChoiceResponse>>
{
    private const int MaxChoices = 25; // Discord's own cap on autocomplete results.

    /// <inheritdoc/>
    public async Task<Result<List<RaidEventChoiceResponse>>> HandleAsync(GetUpcomingPublishedRaidEventChoicesQuery query, CancellationToken cancellationToken)
    {
        var events = await raidEventRepository.GetUpcomingPublishedForGuildAsync(query.GuildId, DateTime.UtcNow, MaxChoices, cancellationToken);
        var guild = await guildsRepository.GetByIdAsync(query.GuildId, cancellationToken);

        var response = events.Select(e => new RaidEventChoiceResponse
        {
            Id = e.Id,
            GuildBranchId = e.GuildBranchId,
            Name = e.Name,
            StartsAtLocal = GuildTimeHelper.ToGuildLocalDateTime(e.StartsAtUtc, guild?.Timezone),
            BranchName = e.GuildBranch.Branch.Name,
        }).ToList();

        return Result<List<RaidEventChoiceResponse>>.Ok(response);
    }
}
