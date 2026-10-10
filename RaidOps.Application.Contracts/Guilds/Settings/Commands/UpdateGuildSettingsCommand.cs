using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Guilds.Settings.Commands;

/// <summary>
/// Command that persists the guild-level identity settings (timezone, language) for a registered
/// guild. Roster/officer role-set configuration is per-branch now — see
/// <see cref="Branches.Commands.UpdateGuildBranchRosterSettingsCommand"/>.
/// The requesting user must be an admin of the target guild.
/// </summary>
public class UpdateGuildSettingsCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the guild to configure. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the user applying the settings. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>IANA timezone identifier (e.g. <c>"Europe/Paris"</c>).</summary>
    public required string Timezone { get; set; }

    /// <summary>Language RaidOps communicates in for this guild (e.g. Discord bot messages) — "en", "fr", or "de".</summary>
    public required string Language { get; set; }
}
