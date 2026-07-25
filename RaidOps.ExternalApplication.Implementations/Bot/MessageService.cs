using NetCord.Gateway;
using NetCord.Rest;
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

    /// <inheritdoc/>
    public async Task SendEmbedAsync(ulong channelId, DiscordEmbedContent embed, CancellationToken cancellationToken = default)
    {
        var embedProperties = new EmbedProperties()
            .WithTitle(embed.Title)
            .WithDescription(embed.Description);

        if (embed.ColorHex.HasValue)
            embedProperties.WithColor(new NetCord.Color(embed.ColorHex.Value));

        if (embed.Fields is { Count: > 0 })
        {
            embedProperties.WithFields(embed.Fields.Select(f =>
                new EmbedFieldProperties().WithName(f.Name).WithValue(f.Value)));
        }

        if (embed.FooterText is not null)
            embedProperties.WithFooter(new EmbedFooterProperties().WithText(embed.FooterText));

        if (embed.Url is not null)
            embedProperties.WithUrl(embed.Url);

        if (embed.Author is not null)
        {
            embedProperties.WithAuthor(new EmbedAuthorProperties()
                .WithName(embed.Author.Name)
                .WithIconUrl(embed.Author.IconUrl));
        }

        var message = new MessageProperties().WithEmbeds([embedProperties]);

        await gatewayClient.Rest.SendMessageAsync(channelId, message, cancellationToken: cancellationToken);
    }
}
