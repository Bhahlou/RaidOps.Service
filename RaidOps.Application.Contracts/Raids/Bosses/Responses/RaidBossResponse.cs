namespace RaidOps.Application.Contracts.Raids.Bosses.Responses;

/// <summary>A boss encounter, self-contained with its zone's display info so callers don't need a second lookup.</summary>
public class RaidBossResponse
{
    /// <summary>Internal raid boss ID.</summary>
    public required int Id { get; set; }

    /// <summary>Display name (e.g. "Hydross the Unstable").</summary>
    public required string Name { get; set; }

    /// <summary>Icon URL (boss portrait), or <c>null</c> if none is configured.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Display/pull ordering within its zone.</summary>
    public required int SortOrder { get; set; }

    /// <summary>Internal ID of the zone this boss belongs to.</summary>
    public required int RaidZoneId { get; set; }

    /// <summary>Display name of the zone this boss belongs to (e.g. "Serpentshrine Cavern").</summary>
    public required string RaidZoneName { get; set; }

    /// <summary>Short code of the zone this boss belongs to (e.g. "SSC").</summary>
    public required string RaidZoneShortCode { get; set; }
}
