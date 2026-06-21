using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Specs.Responses;

namespace RaidOps.Application.Contracts.Specs.Queries;

/// <summary>
/// Query that returns all WoW specs ordered by Blizzard spec ID.
/// Used to populate class-constrained spec pickers on the front end.
/// </summary>
public class GetSpecsQuery : IQueryRequest<IEnumerable<SpecDto>>;
