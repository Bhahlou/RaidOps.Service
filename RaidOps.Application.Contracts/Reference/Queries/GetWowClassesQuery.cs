using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Reference.Responses;

namespace RaidOps.Application.Contracts.Reference.Queries;

/// <summary>Query that returns all WoW classes ordered by Blizzard class ID. Used to populate expansion-filtered class pickers on the front end.</summary>
public class GetWowClassesQuery : IQueryRequest<IEnumerable<WowClassDto>>
{
    /// <summary>
    /// When set, only returns classes actually available on this expansion — see
    /// <see cref="RaidOps.Domain.Models.Reference.Expansion.IsContentAvailableFrom"/>. Leave null to
    /// return every seeded class unfiltered.
    /// </summary>
    public int? AvailableForExpansionId { get; init; }
}
