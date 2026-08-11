namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>Minimal identity of a raid event — backs the raid detail page's breadcrumb, see <see cref="Queries.GetRaidEventSummaryQuery"/>.</summary>
public class RaidEventSummaryResponse
{
    /// <summary>Surrogate ID of the raid event.</summary>
    public required int Id { get; set; }

    /// <summary>Display name (e.g. "Split 1").</summary>
    public required string Name { get; set; }
}
