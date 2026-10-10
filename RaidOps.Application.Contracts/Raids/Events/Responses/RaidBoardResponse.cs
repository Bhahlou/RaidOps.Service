namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>Every raid event of a guild within a requested date range. Returned by <c>GetRaidBoardQuery</c>.</summary>
public class RaidBoardResponse
{
    /// <summary>The events starting within the requested range, ordered by start time.</summary>
    public required List<RaidEventResponse> Events { get; set; }
}
