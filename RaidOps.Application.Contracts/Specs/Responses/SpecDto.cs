namespace RaidOps.Application.Contracts.Specs.Responses;

/// <summary>
/// Lightweight representation of a WoW spec returned by <c>GET /api/v1/specs</c>.
/// Used by the front end to render class-constrained spec pickers.
/// </summary>
public class SpecDto
{
    /// <summary>Blizzard spec ID.</summary>
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Arms", "Devastation").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The raid/group role this spec fills: "Tank", "Healer", or "Dps".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>FK to the class this spec belongs to.</summary>
    public int ClassId { get; set; }

    /// <summary>Icon URL from the Blizzard CDN. <c>null</c> if not yet synced.</summary>
    public string? IconUrl { get; set; }
}
