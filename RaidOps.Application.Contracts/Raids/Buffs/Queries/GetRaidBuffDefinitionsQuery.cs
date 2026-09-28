using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;

namespace RaidOps.Application.Contracts.Raids.Buffs.Queries;

/// <summary>Returns the curated raid buff/debuff definitions of one expansion — read by the raid composition preview and by the admin screen.</summary>
public class GetRaidBuffDefinitionsQuery : IQueryRequest<List<RaidBuffDefinitionResponse>>
{
    /// <summary>The expansion whose definitions to return.</summary>
    public required int ExpansionId { get; set; }
}
