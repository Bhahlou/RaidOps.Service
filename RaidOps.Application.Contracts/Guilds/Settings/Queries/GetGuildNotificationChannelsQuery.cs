using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;

namespace RaidOps.Application.Contracts.Guilds.Settings.Queries;

/// <summary>
/// Query that returns the guild's text-postable Discord channels, from the bot's Gateway cache,
/// each annotated with whether the bot currently has permission to post there. Used to populate
/// the channel picker in the notification settings tab. The requesting user must be an admin of
/// the target guild.
/// </summary>
public class GetGuildNotificationChannelsQuery : IQueryRequest<List<DiscordChannelResponse>>
{
    /// <summary>The Discord snowflake ID of the guild whose channels to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
