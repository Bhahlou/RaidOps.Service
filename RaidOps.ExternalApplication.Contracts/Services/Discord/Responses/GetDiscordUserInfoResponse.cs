using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;

public class GetDiscordUserInfoResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    /// <summary>
    /// global_name is the custom display name and is null for accounts that never set one;
    /// username is always present, so it's the safe fallback.
    /// </summary>
    public string DisplayName => GlobalName ?? Username;
}
