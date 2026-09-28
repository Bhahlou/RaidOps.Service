using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs.Queries;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;
using RaidOps.Application.Implementations.Raids.Buffs.Services;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Buffs.QueryHandlers;

/// <summary>Handles <see cref="GetRaidBuffDefinitionsQuery"/> by reading the curated definitions of one expansion. Reference data, so any authenticated user may read it.</summary>
public class GetRaidBuffDefinitionsQueryHandler(IRaidBuffDefinitionsRepository definitionsRepository)
    : IQueryHandlerAsync<GetRaidBuffDefinitionsQuery, List<RaidBuffDefinitionResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidBuffDefinitionResponse>>> HandleAsync(GetRaidBuffDefinitionsQuery query, CancellationToken cancellationToken)
    {
        var definitions = await definitionsRepository.GetForExpansionAsync(query.ExpansionId, cancellationToken);

        return Result<List<RaidBuffDefinitionResponse>>.Ok(definitions.Select(RaidBuffDefinitionMapper.ToResponse).ToList());
    }
}
