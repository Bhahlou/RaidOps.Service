using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Reference.Queries;
using RaidOps.Application.Contracts.Reference.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Reference.QueryHandlers;

/// <summary>Handles <see cref="GetWowClassesQuery"/> by reading the seeded class reference table.</summary>
public class GetWowClassesQueryHandler(IWowClassRepository wowClassRepository)
    : IQueryHandlerAsync<GetWowClassesQuery, IEnumerable<WowClassDto>>
{
    /// <summary>Returns all classes ordered by Blizzard ID, mapped to lightweight <see cref="WowClassDto"/> objects.</summary>
    public async Task<Result<IEnumerable<WowClassDto>>> HandleAsync(GetWowClassesQuery query, CancellationToken cancellationToken)
    {
        var classes = await wowClassRepository.GetAllAsync(cancellationToken);

        var dtos = classes.Select(c => new WowClassDto
        {
            Id = c.Id,
            Name = c.Name,
            Color = c.Color,
            FirstExpansionId = c.FirstExpansionId,
        });

        return Result<IEnumerable<WowClassDto>>.Ok(dtos);
    }
}
