namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// A text-postable Discord channel, as seen from the bot's Gateway cache, along with whether the
/// bot currently has permission to post messages in it.
/// </summary>
/// <param name="ChannelId">Discord snowflake ID of the channel.</param>
/// <param name="Name">Channel name.</param>
/// <param name="MissingPermissions">
/// Which of <c>ViewChannel</c>, <c>SendMessages</c>, and <c>EmbedLinks</c> the bot currently lacks
/// on this channel — the latter is required since notifications are sent as embeds — computed from
/// its role/overwrite permissions at read time (not cached). Empty when the bot can post there.
/// </param>
/// <param name="CategoryName">
/// Name of the category this channel is nested under, or <c>null</c> if it isn't in one —
/// disambiguates same-named channels living in different categories.
/// </param>
public record DiscordChannelInfo(ulong ChannelId, string Name, IReadOnlyList<DiscordChannelPermissionFlag> MissingPermissions, string? CategoryName = null);
