using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>
/// Response returned by the Battle.net OAuth2 userinfo endpoint
/// (<c>GET https://{region}.battle.net/oauth/userinfo</c>).
/// </summary>
public class BnetUserInfoResponse
{
    /// <summary>Blizzard's internal numeric account ID (also returned as the <c>sub</c> claim).</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>The user's BattleTag (e.g. "Username#1234").</summary>
    [JsonPropertyName("battletag")]
    public string BattleTag { get; set; } = string.Empty;

    /// <summary>Subject claim — same value as <see cref="Id"/> in string form.</summary>
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;
}
