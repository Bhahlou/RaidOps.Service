using NetCord.Gateway;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Facade that exposes Discord bot capabilities to the application layer.
/// Delegates to <see cref="GuildService"/> and <see cref="MessageService"/>,
/// both of which read from the <see cref="GatewayClient"/> in-memory cache.
/// </summary>
public class DiscordBotService(GatewayClient gatewayClient) : IDiscordBotService
{
    /// <inheritdoc/>
    public IGuildService Guilds { get; } = new GuildService(gatewayClient);

    /// <inheritdoc/>
    public IMessageService Messages { get; } = new MessageService(gatewayClient);
}
