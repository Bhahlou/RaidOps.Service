using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.QueryHandlers;

/// <summary>Handles <see cref="GetGuildAttributionDefinitionsQuery"/> by returning a guild branch's raid-attribution template.</summary>
public class GetGuildAttributionDefinitionsQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IGuildAttributionDefinitionsRepository definitionsRepository)
    : IQueryHandlerAsync<GetGuildAttributionDefinitionsQuery, List<GuildAttributionDefinitionResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<GuildAttributionDefinitionResponse>>> HandleAsync(GetGuildAttributionDefinitionsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<GuildAttributionDefinitionResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var expansionId = await guildBranchesRepository.GetCurrentExpansionIdAsync(query.GuildId, query.GuildBranchId, cancellationToken);
        if (expansionId is null)
            return Result<List<GuildAttributionDefinitionResponse>>.Fail(ResponseDetail.GuildBranchNotFound, "Guild branch not found.");

        var definitions = await definitionsRepository.GetForBranchAsync(query.GuildId, query.GuildBranchId, query.RaidBossId, cancellationToken);

        var response = definitions.Select(d => AttributionCellMapper.ToDefinitionResponse(d, expansionId.Value)).ToList();

        return Result<List<GuildAttributionDefinitionResponse>>.Ok(response);
    }
}
