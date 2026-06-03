using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>A talent loadout (active or inactive).</summary>
public class BnetSpecializationGroupDto
{
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("specializations")]
    public List<BnetSpecializationTreeDto> Specializations { get; set; } = [];
}
