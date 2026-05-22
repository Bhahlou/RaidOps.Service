using NetCord.Gateway;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Sends messages to Discord channels via the bot's REST client,
/// which is exposed through the <see cref="GatewayClient"/>.
/// </summary>
public class MessageService(GatewayClient gatewayClient) : IMessageService
{
    /// <inheritdoc/>
    public async Task SendMessageAsync(ulong channelId, string message, CancellationToken cancellationToken = default)
    {
        var channel = await gatewayClient.Rest.GetChannelAsync(channelId, cancellationToken: cancellationToken);
        await gatewayClient.Rest.SendMessageAsync(channel.Id, message, cancellationToken: cancellationToken);
    }
}
