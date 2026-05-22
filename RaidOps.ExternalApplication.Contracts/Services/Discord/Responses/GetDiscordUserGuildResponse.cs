using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;

public class GetDiscordUserGuildResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("owner")]
    public bool Owner { get; set; }

    /// <summary>
    /// Permissions bitfield as string (Discord API v8+, 64-bit).
    /// </summary>
    [JsonPropertyName("permissions")]
    public string? Permissions { get; set; }

    /// <summary>
    /// True if the user is owner or has the Administrator permission (bit 3 = 0x8).
    /// </summary>
    public bool IsAdmin => Owner || (long.TryParse(Permissions, out var perms) && (perms & 0x8) == 0x8);
}
