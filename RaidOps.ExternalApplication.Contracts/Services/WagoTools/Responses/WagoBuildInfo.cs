using RaidOps.ExternalApplication.Contracts.Services.WagoTools;
using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.WagoTools.Responses;

/// <summary>
/// One product entry from <c>GET https://wago.tools/api/builds/latest</c> — the current build
/// Blizzard has shipped for that product (e.g. <c>wow_classic_beta</c> for WoW Forever).
/// </summary>
public class WagoBuildInfo
{
    /// <summary>The wago.tools product code, e.g. <c>"wow_classic_beta"</c>.</summary>
    [JsonPropertyName("product")]
    public string Product { get; set; } = string.Empty;

    /// <summary>The full build version, e.g. <c>"1.60.1.69977"</c>.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>When Blizzard shipped this build (UTC).</summary>
    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(WagoDateTimeConverter))]
    public DateTime CreatedAt { get; set; }
}
