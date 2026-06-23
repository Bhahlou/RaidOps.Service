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
    /// Indicates whether the guild settings (timezone and roster mode) have been configured.
    /// A guild can be registered but not yet configured if the admin invited the bot but did not
    /// complete the settings step.
    /// </summary>
    public bool IsConfigured { get; set; }

    /// <summary>
    /// Indicates whether the current user holds admin rights on this Discord server.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// The user's access level on this guild (Public/Roster/Officer), as computed by
    /// <see cref="RaidOps.Application.Contracts.Services.IGuildAccessService"/>.
    /// </summary>
    public GuildAccessLevel AccessLevel { get; set; }
}
