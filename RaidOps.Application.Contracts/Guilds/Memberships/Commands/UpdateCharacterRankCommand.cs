using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Commands;

/// <summary>
/// Command to update the raid-composition rank of a character already on a guild roster.
/// The requesting user must own the character.
/// </summary>
public class UpdateCharacterRankCommand : ICommandRequest
{
    /// <summary>Internal ID of the character. Set by the controller, not from the request body.</summary>
    public int CharacterId { get; set; }

    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>The new raid-composition rank to assign.</summary>
    public CharacterRank CharacterRank { get; set; }
}
