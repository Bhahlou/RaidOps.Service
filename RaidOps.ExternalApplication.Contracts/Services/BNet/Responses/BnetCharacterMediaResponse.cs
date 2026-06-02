using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>Response envelope for <c>GET /profile/wow/character/{realm}/{name}/character-media</c>.</summary>
public class BnetCharacterMediaResponse
{
    [JsonPropertyName("assets")]
    public List<BnetMediaAssetDto> Assets { get; set; } = [];
}
