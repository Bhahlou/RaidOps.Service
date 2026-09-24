using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;

/// <summary>Response of <c>GET https://wago.tools/api/info/{fileDataId}</c>.</summary>
public class WagoFileInfoResponse
{
    /// <summary>The file's real path in Blizzard's client, e.g. <c>"interface/icons/inv_misc_food_59.blp"</c>.</summary>
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;
}
