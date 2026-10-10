namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>Lightweight spec reference — enough to render an icon and a picker label, no role/class detail.</summary>
public class RaidSpecRefResponse
{
    /// <summary>Blizzard specialisation ID.</summary>
    public required int Id { get; set; }

    /// <summary>Display name (e.g. "Arms", "Feral").</summary>
    public required string Name { get; set; }

    /// <summary>Icon URL, or <c>null</c> if none is configured.</summary>
    public string? IconUrl { get; set; }
}
