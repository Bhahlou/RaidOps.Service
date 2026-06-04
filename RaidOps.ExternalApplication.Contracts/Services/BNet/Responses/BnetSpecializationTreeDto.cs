using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>A single talent tree within a loadout (e.g. Arms, Fury, Protection).</summary>
public class BnetSpecializationTreeDto
{
    [JsonPropertyName("specialization_name")]
    public string SpecializationName { get; set; } = string.Empty;

    [JsonPropertyName("spent_points")]
    public int SpentPoints { get; set; }

    [JsonPropertyName("talents")]
    public List<BnetTreeTalentEntryDto> Talents { get; set; } = [];
}
