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

    /// <summary>
    /// Whether the user is the actual owner of this Discord server, as reported by Discord's
    /// <c>owner</c> flag. Tracked separately from <see cref="IsAdmin"/> (which also becomes
    /// <c>true</c> for any Administrator, owner or not) so nobody — not even another admin —
    /// can ever outrank the real owner in <c>IGuildAccessService.OutranksAsync</c>.
    /// </summary>
    public bool IsOwner { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The user side of the membership.</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>The guild side of the membership.</summary>
    public virtual Guild Guild { get; set; } = null!;
}
