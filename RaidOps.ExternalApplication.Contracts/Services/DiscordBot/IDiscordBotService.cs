namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// Facade that exposes the Discord bot capabilities used by the application layer.
/// Backed by a live <see cref="NetCord.Gateway.GatewayClient"/> connection.
/// </summary>
public interface IDiscordBotService
{
    /// <summary>Operations on Discord guilds (members, roles, etc.).</summary>
    IGuildService Guilds { get; }

    /// <summary>Operations for sending messages to Discord channels.</summary>
    IMessageService Messages { get; }
}
