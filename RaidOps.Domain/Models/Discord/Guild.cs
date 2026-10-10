using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// A Discord server (guild) that has been seen or registered in RaidOps.
/// The Discord snowflake ID is used as primary key.
/// </summary>
[Table("Guilds")]
public class Guild
{
    /// <summary>Discord snowflake ID — primary key.</summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    /// <summary>Discord server name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Discord icon hash, or <c>null</c> if the guild has no custom icon.</summary>
    public string? IconHash { get; set; }

    /// <summary>
    /// Indicates whether this guild has been registered and configured in RaidOps.
    /// Only registered guilds unlock the full feature set (raid planning, roster, loot).
    /// </summary>
    public bool IsRegistered { get; set; }

    /// <summary>
    /// IANA timezone identifier for this guild (e.g. "Europe/Paris").
    /// Null until the guild owner completes the settings step of the registration flow.
    /// </summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// Language RaidOps communicates in for this guild (e.g. Discord bot messages) — one of the
    /// front-end's supported locale codes ("en", "fr", "de"). Pre-filled from the Discord guild's
    /// <c>preferred_locale</c> at registration when it maps to a supported language (only
    /// meaningful for Community-enabled Discord servers; defaults to "en" otherwise), and
    /// editable afterwards from guild settings.
    /// </summary>
    public string? Language { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>Discord user memberships associated with this guild.</summary>
    public virtual ICollection<UserGuild> UserGuilds { get; set; } = [];

    /// <summary>Character roster memberships for this guild.</summary>
    public virtual ICollection<GuildMembership> Memberships { get; set; } = [];

    /// <summary>WoW game-version branches activated on this guild (active and deactivated).</summary>
    public virtual ICollection<GuildBranch> Branches { get; set; } = [];
}
