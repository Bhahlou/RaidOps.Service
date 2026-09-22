namespace RaidOps.Application.Contracts.Reference.Responses;

/// <summary>
/// Lightweight representation of a WoW class returned by <c>GET /api/v1/wowclasses</c>.
/// Used by the front end to render expansion-filtered class pickers.
/// </summary>
public class WowClassDto
{
    /// <summary>Blizzard class ID.</summary>
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Death Knight", "Demon Hunter").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Official class colour as a 6-character hex string (no leading #).</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>The expansion in which this class first became playable — lets the front filter out classes not yet available.</summary>
    public int FirstExpansionId { get; set; }
}
