using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;

namespace RaidOps.Application.Contracts.Guilds.Settings.Queries;

/// <summary>
/// Query that returns the current settings (timezone, roster mode, role threshold) of a registered guild.
/// </summary>
public class GetGuildSettingsQuery : IQueryRequest<GuildSettingsResponse>
{
    /// <summary>The Discord snowflake ID of the guild whose settings to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
