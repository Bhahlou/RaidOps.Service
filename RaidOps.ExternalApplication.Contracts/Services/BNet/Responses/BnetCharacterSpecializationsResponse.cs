using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>Response envelope for <c>GET /profile/wow/character/{realm}/{name}/specializations</c>.</summary>
public class BnetCharacterSpecializationsResponse
{
    /// <summary>
    /// Present on MoP / Retail: the currently active specialisation.
    /// <c>null</c> on Classic / TBC where talent trees are used instead.
    /// </summary>
    [JsonPropertyName("active_specialization")]
    public BnetIdRefDto? ActiveSpecialization { get; set; }

    /// <summary>
    /// Present on MoP / Retail: all specs the character has set up talents for (active + offspec).
    /// </summary>
    [JsonPropertyName("specializations")]
    public List<BnetSpecializationEntryDto> Specializations { get; set; } = [];

    /// <summary>
    /// Present on Classic / TBC: talent tree loadouts.
    /// On MoP this field only contains glyph data — ignore it for spec resolution.
    /// </summary>
    [JsonPropertyName("specialization_groups")]
    public List<BnetSpecializationGroupDto> SpecializationGroups { get; set; } = [];
}
