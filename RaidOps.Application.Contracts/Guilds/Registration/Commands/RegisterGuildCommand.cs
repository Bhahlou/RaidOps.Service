using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Registration.Commands;

/// <summary>
/// Command that marks a Discord guild as registered in RaidOps.
/// The requesting user must be an admin of the target guild.
/// </summary>
public class RegisterGuildCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild to register.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the user initiating the registration.</summary>
    public required string RequesterDiscordId { get; set; }
}
