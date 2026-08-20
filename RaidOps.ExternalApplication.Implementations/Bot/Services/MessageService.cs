using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot.Services;

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
        var message = new MessageProperties().WithEmbeds([BuildEmbedProperties(embed)]);
        await gatewayClient.Rest.SendMessageAsync(channelId, message, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendMessageWithEmbedAsync(ulong channelId, string message, DiscordEmbedContent embed, CancellationToken cancellationToken = default)
    {
        var properties = new MessageProperties().WithContent(message).WithEmbeds([BuildEmbedProperties(embed)]);
        await gatewayClient.Rest.SendMessageAsync(channelId, properties, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ulong> PostEmbedAsync(ulong channelId, DiscordEmbedContent embed, CancellationToken cancellationToken = default)
    {
        var message = new MessageProperties().WithEmbeds([BuildEmbedProperties(embed)]);
        ApplyComponents(message, embed);
        var posted = await gatewayClient.Rest.SendMessageAsync(channelId, message, cancellationToken: cancellationToken);
        return posted.Id;
    }

    /// <inheritdoc/>
    public async Task EditEmbedAsync(ulong channelId, ulong messageId, DiscordEmbedContent embed, CancellationToken cancellationToken = default)
    {
        var embedProperties = BuildEmbedProperties(embed);
        var components = embed.Buttons is { Count: > 0 }
            ? new[] { new ActionRowProperties(embed.Buttons.Select(BuildButtonProperties)) }
            : [];

        await gatewayClient.Rest.ModifyMessageAsync(
            channelId, messageId,
            options => options.WithEmbeds([embedProperties]).WithComponents(components),
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendDirectMessageEmbedAsync(ulong discordUserId, DiscordEmbedContent embed, CancellationToken cancellationToken = default)
    {
        var dmChannel = await gatewayClient.Rest.GetDMChannelAsync(discordUserId, cancellationToken: cancellationToken);
        var message = new MessageProperties().WithEmbeds([BuildEmbedProperties(embed)]);
        await gatewayClient.Rest.SendMessageAsync(dmChannel.Id, message, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteMessageAsync(ulong channelId, ulong messageId, CancellationToken cancellationToken = default) =>
        await gatewayClient.Rest.DeleteMessageAsync(channelId, messageId, cancellationToken: cancellationToken);

    private static EmbedProperties BuildEmbedProperties(DiscordEmbedContent embed)
    {
        var embedProperties = new EmbedProperties()
            .WithTitle(embed.Title)
            .WithDescription(embed.Description);

        if (embed.ColorHex.HasValue)
            embedProperties.WithColor(new NetCord.Color(embed.ColorHex.Value));

        if (embed.Fields is { Count: > 0 })
        {
            embedProperties.WithFields(embed.Fields.Select(f =>
                new EmbedFieldProperties().WithName(f.Name).WithValue(f.Value).WithInline(f.Inline)));
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

        return embedProperties;
    }

    private static void ApplyComponents(MessageProperties message, DiscordEmbedContent embed)
    {
        if (embed.Buttons is not { Count: > 0 })
            return;

        message.WithComponents([new ActionRowProperties(embed.Buttons.Select(BuildButtonProperties))]);
    }

    private static ButtonProperties BuildButtonProperties(DiscordEmbedButton button) =>
        new(button.CustomId, button.Label, MapStyle(button.Style));

    private static ButtonStyle MapStyle(DiscordEmbedButtonStyle style) => style switch
    {
        DiscordEmbedButtonStyle.Primary => ButtonStyle.Primary,
        DiscordEmbedButtonStyle.Success => ButtonStyle.Success,
        DiscordEmbedButtonStyle.Danger => ButtonStyle.Danger,
        _ => ButtonStyle.Secondary,
    };
}
