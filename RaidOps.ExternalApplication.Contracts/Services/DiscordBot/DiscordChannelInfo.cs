namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// A text-postable Discord channel, as seen from the bot's Gateway cache, along with whether the
/// bot currently has permission to post messages in it.
/// </summary>
/// <param name="ChannelId">Discord snowflake ID of the channel.</param>
/// <param name="Name">Channel name.</param>
/// <param name="BotCanSendMessages">
/// Whether the bot currently has both <c>ViewChannel</c> and <c>SendMessages</c> permissions on
/// this channel, computed from its role/overwrite permissions at read time (not cached).
/// </param>
/// <param name="CategoryName">
/// Name of the category this channel is nested under, or <c>null</c> if it isn't in one —
/// disambiguates same-named channels living in different categories.
/// </param>
public record DiscordChannelInfo(ulong ChannelId, string Name, bool BotCanSendMessages, string? CategoryName = null);
