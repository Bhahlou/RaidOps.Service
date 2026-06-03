using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// Junction table linking a <see cref="User"/> to a Discord <see cref="Guild"/>.
/// Tracks whether the user holds admin permissions on that server.
/// Composite primary key: (<see cref="UserDiscordId"/>, <see cref="GuildId"/>).
/// </summary>
[Table("UserGuilds")]
public class UserGuild
{
    /// <summary>Discord snowflake ID of the user.</summary>
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the guild.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user has the Administrator permission on this Discord server,
    /// which qualifies them to register the guild in RaidOps.
    /// </summary>
    public bool IsAdmin { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The user side of the membership.</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>The guild side of the membership.</summary>
    public virtual Guild Guild { get; set; } = null!;
}
