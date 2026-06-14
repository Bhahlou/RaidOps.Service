using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Responses;

/// <summary>
/// Represents one of the requesting user's characters that is on a guild's roster.
/// Returned by <c>GetMyMembershipsInGuildQuery</c>.
/// </summary>
public class CharacterInGuildResponse
{
    /// <summary>Internal character ID.</summary>
    public required int CharacterId { get; set; }

    /// <summary>Character name.</summary>
    public required string Name { get; set; }

    /// <summary>Realm name.</summary>
    public required string RealmName { get; set; }

    /// <summary>Class name.</summary>
    public required string ClassName { get; set; }

    /// <summary>Class colour hex string (without #).</summary>
    public required string ClassColor { get; set; }

    /// <summary>Avatar image URL, or <c>null</c> if not yet fetched.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>WoW in-game guild name from the active expansion state, or <c>null</c> if unguilded.</summary>
    public string? GuildName { get; set; }

    /// <summary>Raid-composition rank of this character on the roster.</summary>
    public required CharacterRank CharacterRank { get; set; }

    /// <summary>UTC timestamp of when the character joined the roster.</summary>
    public required DateTime JoinedAt { get; set; }
}
