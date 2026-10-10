using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;

namespace RaidOps.Application.Contracts.Guilds.Settings.Queries;

/// <summary>
/// Query that returns the guild's Discord channel categories, from the bot's Gateway cache — lets
/// an officer pick where a bot-created channel should be nested (e.g. a dedicated per-raid
/// announcement channel). The requesting user must be an admin of the target guild.
/// </summary>
public class GetGuildCategoriesQuery : IQueryRequest<GuildCategoriesResponse>
{
    /// <summary>The Discord snowflake ID of the guild whose categories to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
