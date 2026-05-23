using System.Text.Json.Serialization;

namespace RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

/// <summary>Response envelope for <c>GET /profile/user/wow</c>.</summary>
public class BnetWowAccountsResponse
{
    [JsonPropertyName("wow_accounts")]
    public List<BnetWowAccountDto> WowAccounts { get; set; } = [];
}

/// <summary>One WoW account (a player can have multiple).</summary>
public class BnetWowAccountDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("characters")]
    public List<BnetWowCharacterDto> Characters { get; set; } = [];
}

/// <summary>A single WoW character as returned by the account summary endpoint.</summary>
public class BnetWowCharacterDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("realm")]
    public BnetRealmRefDto Realm { get; set; } = null!;

    [JsonPropertyName("playable_class")]
    public BnetIdRefDto PlayableClass { get; set; } = null!;

    [JsonPropertyName("playable_race")]
    public BnetIdRefDto PlayableRace { get; set; } = null!;

    [JsonPropertyName("faction")]
    public BnetTypeRefDto Faction { get; set; } = null!;

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

/// <summary>Realm reference embedded in a character summary.</summary>
public class BnetRealmRefDto
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Generic id+name reference (class, race…).</summary>
public class BnetIdRefDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Generic type+name reference (faction, gender…).</summary>
public class BnetTypeRefDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
