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

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>User memberships associated with this guild.</summary>
    public virtual ICollection<UserGuild> UserGuilds { get; set; } = [];
}
