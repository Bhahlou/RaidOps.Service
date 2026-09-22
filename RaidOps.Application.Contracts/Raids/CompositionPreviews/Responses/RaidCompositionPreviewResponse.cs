namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;

/// <summary>
/// Full detail of a raid composition preview, backing the composer page. <see cref="Slots"/> is
/// sparse — the front end reconstructs the full grid from <see cref="GroupCount"/> x
/// <see cref="SlotsPerGroup"/> and looks up each coordinate by position.
/// </summary>
public class RaidCompositionPreviewResponse
{
    /// <summary>Surrogate ID.</summary>
    public required int Id { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>Number of groups in the grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>The filled slots of the grid — empty coordinates are simply absent.</summary>
    public required List<RaidCompositionPreviewSlotResponse> Slots { get; set; }
}
