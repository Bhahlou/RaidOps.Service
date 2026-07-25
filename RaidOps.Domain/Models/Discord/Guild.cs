using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;

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
    /// Controls who may join the guild's roster.
    /// Null until the guild owner completes the settings step of the registration flow.
    /// </summary>
    public RosterMode? RosterMode { get; set; }

    /// <summary>
    /// Discord snowflake ID of the minimum role required to join the roster.
    /// Members with this role <em>or any role with a higher position</em> are granted access.
    /// Only relevant when <see cref="RosterMode"/> is <see cref="RosterMode.DiscordRoleOnly"/>.
    /// </summary>
    public string? MinRosterRoleId { get; set; }

    /// <summary>
    /// Discord snowflake ID of the minimum role that grants Officer access in RaidOps.
    /// Members with this role <em>or any role with a higher position</em> are granted Officer
    /// access, independently of <see cref="RosterMode"/>. Null until the admin explicitly saves
    /// a choice — every guild is expected to designate one (the Discord Administrator/owner
    /// safety net always applies on top, so this can never lock an admin out).
    /// </summary>
    public string? MinOfficerRoleId { get; set; }

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
}
