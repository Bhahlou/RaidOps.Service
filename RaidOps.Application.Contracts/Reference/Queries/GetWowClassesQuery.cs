using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Reference.Responses;

namespace RaidOps.Application.Contracts.Reference.Queries;

/// <summary>Query that returns all WoW classes ordered by Blizzard class ID. Used to populate expansion-filtered class pickers on the front end.</summary>
public class GetWowClassesQuery : IQueryRequest<IEnumerable<WowClassDto>>;
