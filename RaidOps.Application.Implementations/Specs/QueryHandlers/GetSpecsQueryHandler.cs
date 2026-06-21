using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Specs.Queries;
using RaidOps.Application.Contracts.Specs.Responses;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Specs.QueryHandlers;

/// <summary>
/// Handles <see cref="GetSpecsQuery"/> by reading the seeded spec reference table.
/// </summary>
public class GetSpecsQueryHandler(ISpecRepository specRepository)
    : IQueryHandlerAsync<GetSpecsQuery, IEnumerable<SpecDto>>
{
    /// <summary>
    /// Returns all specs ordered by Blizzard ID, mapped to lightweight <see cref="SpecDto"/> objects.
    /// </summary>
    public async Task<Result<IEnumerable<SpecDto>>> HandleAsync(
        GetSpecsQuery query,
        CancellationToken cancellationToken)
    {
        var specs = await specRepository.GetAllAsync(cancellationToken);

        var dtos = specs.Select(s => new SpecDto
        {
            Id = s.Id,
            Name = s.Name,
            Role = s.Role.ToString(),
            ClassId = s.ClassId,
            IconUrl = s.IconUrl,
        });

        return Result<IEnumerable<SpecDto>>.Ok(dtos);
    }
}
