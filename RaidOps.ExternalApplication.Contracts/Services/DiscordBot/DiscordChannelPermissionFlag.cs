namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// One of the Discord permissions the bot needs on a channel to post notification embeds there —
/// a subset of <c>NetCord.Permissions</c>, kept here so this layer doesn't leak the NetCord type
/// into <see cref="DiscordChannelInfo"/> or its downstream response DTOs.
/// </summary>
public enum DiscordChannelPermissionFlag
{
    ViewChannel,
    SendMessages,
    EmbedLinks,
}
