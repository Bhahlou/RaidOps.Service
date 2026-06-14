using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Memberships.Commands;

/// <summary>
/// Command to remove a character from a guild's RaidOps roster.
/// The requesting user must own the character.
/// </summary>
public class LeaveGuildCommand : ICommandRequest
{
    /// <summary>Internal ID of the character to remove. Set by the controller, not from the request body.</summary>
    public int CharacterId { get; set; }

    /// <summary>Discord snowflake ID of the guild. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;
}
