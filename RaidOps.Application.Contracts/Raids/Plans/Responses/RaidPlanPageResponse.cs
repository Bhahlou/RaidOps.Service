namespace RaidOps.Application.Contracts.Raids.Plans.Responses;

/// <summary>One page of a board's tab bar — no elements, used for the page-tab list.</summary>
public class RaidPlanPageResponse
{
    /// <summary>Surrogate ID of the page.</summary>
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Phase 1").</summary>
    public required string Name { get; set; }

    /// <summary>Display/tab ordering within the board.</summary>
    public int SortOrder { get; set; }

    /// <summary>Key into the front-end's bundled background-image manifest, or <c>null</c> if none picked yet.</summary>
    public string? BackgroundImageKey { get; set; }
}
