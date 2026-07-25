using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Authentication.Responses;

/// <summary>
/// Lightweight representation of a Discord guild linked to the authenticated user,
/// including its RaidOps registration status.
/// </summary>
public class UserGuildResponse
{
    /// <summary>
    /// Gets or sets the Discord snowflake ID of the guild.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the guild's display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the Discord icon hash, or <c>null</c> if the guild has no custom icon.
    /// </summary>
    public string? IconHash { get; set; }

    /// <summary>
    /// Indicates whether the bot has been invited to this guild.
    /// </summary>
    public bool IsRegistered { get; set; }

    /// <summary>
    /// Indicates whether the guild-level identity settings (timezone and language) have been
    /// configured. A guild can be registered but not yet configured if the admin invited the bot
    /// but did not complete the settings step. Independent of whether any WoW branch has been
    /// activated/configured yet — see <see cref="Branches"/>.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Indicates whether the current user holds admin rights on this Discord server.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// The user's active WoW branches on this guild, each with its own access level. Empty if the
    /// guild has no active branch yet (or none the user has any access to).
    /// </summary>
    public List<UserGuildBranchResponse> Branches { get; set; } = [];

    /// <summary>
    /// The user's highest access level anywhere on this guild — <see cref="GuildAccessLevel.Officer"/>
    /// if they're a Discord admin, otherwise the max across <see cref="Branches"/>. Kept for call
    /// sites that only need "does this user have any access to this guild at all" (e.g. sidenav
    /// guild-list visibility) without needing to reason about individual branches.
    /// </summary>
    public GuildAccessLevel AccessLevel { get; set; }
}
