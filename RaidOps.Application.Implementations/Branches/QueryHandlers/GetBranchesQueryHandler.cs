using RaidOps.Application.Contracts.Branches.Queries;
using RaidOps.Application.Contracts.Branches.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Branches.QueryHandlers;

/// <summary>
/// Handles <see cref="GetBranchesQuery"/> by reading the seeded branch reference table.
/// </summary>
public class GetBranchesQueryHandler(IBranchRepository branchRepository)
    : IQueryHandlerAsync<GetBranchesQuery, IEnumerable<BranchDto>>
{
    /// <summary>
    /// Returns all branches ordered by ID, mapped to lightweight <see cref="BranchDto"/> objects.
    /// </summary>
    public async Task<Result<IEnumerable<BranchDto>>> HandleAsync(
        GetBranchesQuery query,
        CancellationToken cancellationToken = default)
    {
        var branches = await branchRepository.GetAllAsync(cancellationToken);

        var dtos = branches.Select(b => new BranchDto
        {
            Id = b.Id,
            Name = b.Name,
            BnetNamespacePrefix = b.BnetNamespacePrefix,
            CurrentExpansionShortCode = b.CurrentExpansion.ShortCode
        });

        return Result<IEnumerable<BranchDto>>.Ok(dtos);
    }
}
