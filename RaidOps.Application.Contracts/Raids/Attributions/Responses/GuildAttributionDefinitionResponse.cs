namespace RaidOps.Application.Contracts.Raids.Attributions.Responses;

/// <summary>A single row of a guild's raid-attribution template.</summary>
public class GuildAttributionDefinitionResponse
{
    /// <summary>Surrogate ID of the definition.</summary>
    public int Id { get; set; }

    /// <summary>Display label.</summary>
    public required string Label { get; set; }

    /// <summary>Free-text grouping label, or <c>null</c> for an ungrouped row.</summary>
    public string? Section { get; set; }

    /// <summary>Whether officers can add a variable number of instances of this row per raid event.</summary>
    public bool IsRepeatable { get; set; }

    /// <summary>The row's ordered cells (icons and name slots).</summary>
    public required List<AttributionCellResponse> Cells { get; set; }

    /// <summary>Display ordering within the guild's template.</summary>
    public int SortOrder { get; set; }
}
