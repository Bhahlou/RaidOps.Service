using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>
/// DTO returned by <see cref="Queries.GetGuildSettingsQuery"/>.
/// </summary>
public class GuildSettingsResponse
{
    /// <summary>IANA timezone identifier (e.g. <c>"Europe/Paris"</c>), or <c>null</c> if not yet configured.</summary>
    public string? Timezone { get; set; }

    /// <summary>Controls who may join the guild's roster.</summary>
    public RosterMode RosterMode { get; set; }

    /// <summary>Discord snowflake ID of the minimum roster role, or <c>null</c> when roster mode is Open.</summary>
    public string? MinRosterRoleId { get; set; }

    /// <summary>Language RaidOps communicates in for this guild ("en", "fr", "de"), or <c>null</c> if not yet resolved.</summary>
    public string? Language { get; set; }
}
