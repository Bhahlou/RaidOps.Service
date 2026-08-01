namespace RaidOps.Application.Contracts.Raids.Zones.Responses;

/// <summary>A lightweight reference to a raid zone, used wherever a series/event's target zones are listed.</summary>
public class RaidZoneRefResponse
{
    /// <summary>Internal raid zone ID.</summary>
    public required int Id { get; set; }

    /// <summary>Display name (e.g. "Serpentshrine Cavern").</summary>
    public required string Name { get; set; }

    /// <summary>Short code for compact UI labels (e.g. "SSC").</summary>
    public required string ShortCode { get; set; }
}
