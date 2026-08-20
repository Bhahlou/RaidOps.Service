using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Queries;
using RaidOps.Application.Contracts.Raids.Events.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Events.QueryHandlers;

/// <inheritdoc cref="GetRaidEventAssignedCharactersQuery"/>
public class GetRaidEventAssignedCharactersQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IRaidCompositionRepository raidCompositionRepository) : IQueryHandlerAsync<GetRaidEventAssignedCharactersQuery, List<RaidEventAssignedCharacterResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidEventAssignedCharacterResponse>>> HandleAsync(GetRaidEventAssignedCharactersQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<RaidEventAssignedCharacterResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var raidEvent = await raidEventRepository.GetByIdAsync(query.EventId, query.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<List<RaidEventAssignedCharacterResponse>>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{query.EventId}' does not exist.");

        var assignments = await raidCompositionRepository.GetAssignmentsForEventAsync(query.EventId, cancellationToken);

        var response = assignments
            .Select(a => new RaidEventAssignedCharacterResponse { CharacterId = a.CharacterId, Name = a.Character.Name })
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<List<RaidEventAssignedCharacterResponse>>.Ok(response);
    }
}
