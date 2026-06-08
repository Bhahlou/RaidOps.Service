using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;

namespace RaidOps.Application.Contracts.Guilds.Settings.Queries;

/// <summary>
/// Query that returns the list of assignable Discord roles for a registered guild.
/// The requesting user must be an admin of the target guild.
/// </summary>
public class GetGuildDiscordRolesQuery : IQueryRequest<List<DiscordRoleResponse>>
{
    /// <summary>The Discord snowflake ID of the guild whose roles to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the user requesting the roles.</summary>
    public required string RequesterDiscordId { get; set; }
}
