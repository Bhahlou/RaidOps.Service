using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Commands;

/// <summary>
/// Command that marks a Discord guild as unregistered in RaidOps.
/// Dispatched automatically when the bot is removed from the Discord server.
/// </summary>
public class UnregisterGuildCommand : ICommandRequest
{
    /// <summary>
    /// The Discord snowflake ID of the guild to unregister.
    /// </summary>
    public required string GuildId { get; set; }
}
