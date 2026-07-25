namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// Provides operations for sending messages to Discord channels via the bot.
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Sends a plain-text message to the specified Discord channel.
    /// </summary>
    /// <param name="channelId">The Discord snowflake ID of the target channel.</param>
    /// <param name="message">The message content to send.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task SendMessageAsync(ulong channelId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a rich embed message to the specified Discord channel.
    /// </summary>
    /// <param name="channelId">The Discord snowflake ID of the target channel.</param>
    /// <param name="embed">The embed content to send.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task SendEmbedAsync(ulong channelId, DiscordEmbedContent embed, CancellationToken cancellationToken = default);
}
