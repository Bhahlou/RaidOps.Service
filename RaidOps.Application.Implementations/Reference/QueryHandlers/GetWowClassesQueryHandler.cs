using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Reference.Queries;
using RaidOps.Application.Contracts.Reference.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Reference.QueryHandlers;

/// <summary>Handles <see cref="GetWowClassesQuery"/> by reading the seeded class reference table.</summary>
public class GetWowClassesQueryHandler(IWowClassRepository wowClassRepository, IExpansionRepository expansionRepository)
    : IQueryHandlerAsync<GetWowClassesQuery, IEnumerable<WowClassDto>>
{
    /// <summary>Returns all classes ordered by Blizzard ID, mapped to lightweight <see cref="WowClassDto"/> objects.</summary>
    public async Task<Result<IEnumerable<WowClassDto>>> HandleAsync(GetWowClassesQuery query, CancellationToken cancellationToken)
    {
        var classes = await wowClassRepository.GetAllAsync(cancellationToken);

        if (query.AvailableForExpansionId is { } targetId)
        {
            var expansionsById = (await expansionRepository.GetAllAsync(cancellationToken)).ToDictionary(e => e.Id);

            if (expansionsById.TryGetValue(targetId, out var target))
            {
                classes = classes.Where(c =>
                    expansionsById.TryGetValue(c.FirstExpansionId, out var origin) && target.IsContentAvailableFrom(origin));
            }
        }

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
