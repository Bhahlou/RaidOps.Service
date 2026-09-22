namespace RaidOps.Application.Contracts.Raids.Plans.Responses;

/// <summary>One page including its elements — the canvas editor's main load response.</summary>
public class RaidPlanPageDetailResponse
{
    /// <summary>Surrogate ID of the page.</summary>
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Phase 1").</summary>
    public required string Name { get; set; }

    /// <summary>Key into the front-end's bundled background-image manifest, or <c>null</c> if none picked yet.</summary>
    public string? BackgroundImageKey { get; set; }

    /// <summary>This page's canvas elements, stacked by <see cref="RaidPlanElementResponse.ZIndex"/>.</summary>
    public required List<RaidPlanElementResponse> Elements { get; set; }
}
