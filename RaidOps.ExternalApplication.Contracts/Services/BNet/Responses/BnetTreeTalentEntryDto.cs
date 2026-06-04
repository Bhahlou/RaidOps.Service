using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>A single talent entry within a Classic talent tree.</summary>
public class BnetTreeTalentEntryDto
{
    [JsonPropertyName("talent")]
    public BnetIdRefDto? Talent { get; set; }
}
