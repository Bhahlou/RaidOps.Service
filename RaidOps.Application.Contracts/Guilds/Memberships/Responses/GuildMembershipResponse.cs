using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Responses;

/// <summary>
/// Represents a guild that a character is currently on the roster of.
/// Returned by <c>GetCharacterMembershipsQuery</c>.
/// </summary>
public class GuildMembershipResponse
{
    /// <summary>Discord snowflake ID of the guild.</summary>
    public required string GuildId { get; set; }

    /// <summary>Name of the guild.</summary>
    public required string GuildName { get; set; }

    /// <summary>Discord icon hash of the guild, or <c>null</c> if no custom icon.</summary>
    public string? GuildIconHash { get; set; }

    /// <summary>Raid-composition rank of this character on the roster.</summary>
    public required CharacterRank CharacterRank { get; set; }

    /// <summary>UTC timestamp of when the character joined the roster.</summary>
    public required DateTime JoinedAt { get; set; }
}
