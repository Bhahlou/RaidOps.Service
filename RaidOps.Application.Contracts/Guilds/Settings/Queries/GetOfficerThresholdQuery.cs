using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;

namespace RaidOps.Application.Contracts.Guilds.Settings.Queries;

/// <summary>
/// Query that returns the guild's current Officer role threshold (the minimum Discord role
/// position that grants Officer access). The requesting user must be an admin of the target guild.
/// </summary>
public class GetOfficerThresholdQuery : IQueryRequest<OfficerThresholdResponse>
{
    /// <summary>The Discord snowflake ID of the guild whose Officer threshold to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
