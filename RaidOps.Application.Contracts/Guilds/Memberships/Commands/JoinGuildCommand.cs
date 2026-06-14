using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Commands;

/// <summary>
/// Command to add a character to a guild's RaidOps roster.
/// The requesting user must own the character and be a Discord member of the guild.
/// Eligibility (RosterMode) is verified by the handler.
/// </summary>
public class JoinGuildCommand : ICommandRequest
{
    /// <summary>Internal ID of the character to add. Set by the controller, not from the request body.</summary>
    public int CharacterId { get; set; }

    /// <summary>Discord snowflake ID of the guild to join. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Initial raid-composition rank for the character on this roster.</summary>
    public CharacterRank CharacterRank { get; set; } = CharacterRank.Main;
}
