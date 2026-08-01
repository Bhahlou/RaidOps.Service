namespace RaidOps.Application.Contracts.Raids.Zones.Responses;

/// <summary>A raid zone available for scheduling. Returned by <c>GetRaidZonesForBranchQuery</c>.</summary>
public class RaidZoneResponse
{
    /// <summary>Internal raid zone ID.</summary>
    public required int Id { get; set; }

    /// <summary>Display name (e.g. "Serpentshrine Cavern").</summary>
    public required string Name { get; set; }

    /// <summary>Short code for compact UI labels (e.g. "SSC").</summary>
    public required string ShortCode { get; set; }

    /// <summary>Number of groups in the raid grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>Icon URL, or <c>null</c> if none is configured.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Display ordering within its expansion.</summary>
    public required int SortOrder { get; set; }
}
