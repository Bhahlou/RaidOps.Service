using RaidOps.Application.Contracts.Branches.Responses;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Branches.Queries;

/// <summary>
/// Query that returns all available WoW branches ordered by ID.
/// Used to populate the branch picker in the character import dialog.
/// </summary>
public class GetBranchesQuery : IQueryRequest<IEnumerable<BranchDto>>;
