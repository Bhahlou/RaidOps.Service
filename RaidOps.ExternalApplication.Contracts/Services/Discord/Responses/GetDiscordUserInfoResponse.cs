using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;

public class GetDiscordUserInfoResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("global_name")]
    public required string Username { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
}
