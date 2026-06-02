using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>Guild reference embedded in a character detail response.</summary>
public class BnetGuildRefDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
