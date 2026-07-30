using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;

namespace RaidOps.Application.Contracts.Guilds.Settings.Queries;

/// <summary>
/// Query that returns the guild's Discord notification settings — one entry per
/// <see cref="RaidOps.Domain.Enums.GuildNotificationEventType"/>, disabled by default when no row
/// has been persisted yet. The requesting user must be an admin of the target guild.
/// </summary>
public class GetGuildNotificationSettingsQuery : IQueryRequest<List<GuildNotificationSettingResponse>>
{
    /// <summary>The Discord snowflake ID of the guild whose notification settings to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>The Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>
    /// The branch to resolve settings for (branch override falling back to the guild-wide row), or
    /// <c>null</c> to read the guild-wide row directly.
    /// </summary>
    public int? GuildBranchId { get; set; }
}
