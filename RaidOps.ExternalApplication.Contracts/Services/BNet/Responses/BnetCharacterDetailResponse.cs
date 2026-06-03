using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>Response envelope for <c>GET /profile/wow/character/{realm}/{name}</c>.</summary>
public class BnetCharacterDetailResponse
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("average_item_level")]
    public int AverageItemLevel { get; set; }

    [JsonPropertyName("equipped_item_level")]
    public int EquippedItemLevel { get; set; }

    [JsonPropertyName("guild")]
    public BnetGuildRefDto? Guild { get; set; }
}
