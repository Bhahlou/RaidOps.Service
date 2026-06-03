using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>
/// An entry in the <c>specializations</c> array of the MoP/Retail specializations endpoint.
/// Represents one spec the character has set up (with talents), whether active or not.
/// </summary>
public class BnetSpecializationEntryDto
{
    [JsonPropertyName("specialization")]
    public BnetIdRefDto Specialization { get; set; } = null!;
}
