namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>
/// DTO returned by <see cref="Queries.GetGuildSettingsQuery"/>. Guild-level identity settings only
/// — roster/officer role-set configuration is per-branch, see
/// <see cref="Branches.Responses.GuildBranchResponse"/>.
/// </summary>
public class GuildSettingsResponse
{
    /// <summary>IANA timezone identifier (e.g. <c>"Europe/Paris"</c>), or <c>null</c> if not yet configured.</summary>
    public string? Timezone { get; set; }

    /// <summary>Language RaidOps communicates in for this guild ("en", "fr", "de"), or <c>null</c> if not yet resolved.</summary>
    public string? Language { get; set; }
}
