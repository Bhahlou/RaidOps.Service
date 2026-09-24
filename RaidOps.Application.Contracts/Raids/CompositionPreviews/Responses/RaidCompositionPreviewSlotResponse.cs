namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;

/// <summary>One filled (group, slot) coordinate of a preview's grid — empty coordinates are simply absent from the list.</summary>
public class RaidCompositionPreviewSlotResponse
{
    /// <summary>1-based group number within the grid.</summary>
    public required int GroupNumber { get; set; }

    /// <summary>1-based slot number within the group.</summary>
    public required int SlotNumber { get; set; }

    /// <summary>Placeholder class ID, or <c>null</c> if unset.</summary>
    public int? WowClassId { get; set; }

    /// <summary>Placeholder class name, or <c>null</c> if unset.</summary>
    public string? WowClassName { get; set; }

    /// <summary>Placeholder class color (6-char hex, no leading #), or <c>null</c> if unset.</summary>
    public string? WowClassColor { get; set; }

    /// <summary>Placeholder spec ID, or <c>null</c> if unset.</summary>
    public int? SpecId { get; set; }

    /// <summary>Placeholder spec name, or <c>null</c> if unset.</summary>
    public string? SpecName { get; set; }

    /// <summary>Placeholder spec icon URL, or <c>null</c> if unset or no icon is configured.</summary>
    public string? SpecIconUrl { get; set; }

    /// <summary>Free-text note, or <c>null</c> if unset.</summary>
    public string? Note { get; set; }
}
