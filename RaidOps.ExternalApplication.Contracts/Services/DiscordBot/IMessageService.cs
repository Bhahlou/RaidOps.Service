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

    /// <summary>
    /// Sends a single message combining plain-text content (e.g. an <c>@mention</c> ping) with a
    /// rich embed, e.g. the grouping ping's composition snapshot — unlike <see cref="PostEmbedAsync"/>,
    /// the returned message isn't meant to be edited later, so no ID is returned.
    /// </summary>
    /// <param name="channelId">The Discord snowflake ID of the target channel.</param>
    /// <param name="message">The message content to send.</param>
    /// <param name="embed">The embed content to attach.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task SendMessageWithEmbedAsync(ulong channelId, string message, DiscordEmbedContent embed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a rich embed message to the specified Discord channel and returns the posted
    /// message's snowflake ID, so it can later be edited in place via <see cref="EditEmbedAsync"/>.
    /// </summary>
    /// <param name="channelId">The Discord snowflake ID of the target channel.</param>
    /// <param name="embed">The embed content to send.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The Discord snowflake ID of the message that was posted.</returns>
    Task<ulong> PostEmbedAsync(ulong channelId, DiscordEmbedContent embed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the embed of an already-sent message in place, e.g. to keep a standing "current
    /// composition" announcement up to date without posting a new message per change.
    /// </summary>
    /// <param name="channelId">The Discord snowflake ID of the channel the message was posted in.</param>
    /// <param name="messageId">The Discord snowflake ID of the message to edit.</param>
    /// <param name="embed">The embed content to replace the message's content with.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task EditEmbedAsync(ulong channelId, ulong messageId, DiscordEmbedContent embed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a rich embed as a direct message to a Discord user, opening/reusing their DM channel
    /// with the bot.
    /// </summary>
    /// <param name="discordUserId">The Discord snowflake ID of the user to message.</param>
    /// <param name="embed">The embed content to send.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task SendDirectMessageEmbedAsync(ulong discordUserId, DiscordEmbedContent embed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a message, e.g. the standing composition announcement when its raid event is deleted.
    /// </summary>
    /// <param name="channelId">The Discord snowflake ID of the channel the message was posted in.</param>
    /// <param name="messageId">The Discord snowflake ID of the message to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task DeleteMessageAsync(ulong channelId, ulong messageId, CancellationToken cancellationToken = default);
}
