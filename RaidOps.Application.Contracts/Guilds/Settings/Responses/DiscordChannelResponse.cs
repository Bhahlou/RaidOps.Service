namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>
/// A text-postable Discord channel, returned by <see cref="Queries.GetGuildNotificationChannelsQuery"/>
/// to populate the notification settings channel picker.
/// </summary>
public class DiscordChannelResponse
{
    /// <summary>Discord snowflake ID of the channel.</summary>
    public required string Id { get; set; }

    /// <summary>Display name of the channel.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether the bot currently has permission to post messages in this channel. The front end
    /// still lists channels where this is <c>false</c>, with a warning, so an admin can pick one
    /// ahead of granting the bot access.
    /// </summary>
    public bool BotCanSendMessages { get; set; }

    /// <summary>Name of the category this channel is nested under, or <c>null</c> if it isn't in one.</summary>
    public string? CategoryName { get; set; }
}
