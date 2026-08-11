using NetCord.Gateway;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot.Services;

/// <summary>
/// Facade that exposes Discord bot capabilities to the application layer.
/// Delegates to <see cref="GuildService"/> and <see cref="MessageService"/>,
/// both of which read from the <see cref="GatewayClient"/> in-memory cache.
/// </summary>
public class DiscordBotService(GatewayClient gatewayClient, IEmojiService emojiService) : IDiscordBotService
{
    /// <inheritdoc/>
    public IGuildService Guilds { get; } = new GuildService(gatewayClient);

    /// <inheritdoc/>
    public IMessageService Messages { get; } = new MessageService(gatewayClient);

    /// <summary>
    /// Injected rather than constructed inline like <see cref="Guilds"/>/<see cref="Messages"/> —
    /// its cache is synced once at bot startup and must survive across every later DI scope, so it
    /// is registered as a singleton and shared, not rebuilt per <see cref="DiscordBotService"/> scope.
    /// </summary>
    public IEmojiService Emojis { get; } = emojiService;
}
