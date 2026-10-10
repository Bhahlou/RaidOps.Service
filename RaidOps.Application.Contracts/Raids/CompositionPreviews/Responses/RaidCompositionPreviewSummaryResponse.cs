namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;

/// <summary>Lightweight preview reference for the list page — no slot detail.</summary>
public class RaidCompositionPreviewSummaryResponse
{
    /// <summary>Surrogate ID.</summary>
    public required int Id { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>Number of groups in the grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>UTC timestamp of the last update, or the creation timestamp if never updated.</summary>
    public required DateTime UpdatedAt { get; set; }
}
