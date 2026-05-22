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
    /// Indicates whether this guild has been registered and configured in RaidOps.
    /// </summary>
    public bool IsRegistered { get; set; }

    /// <summary>
    /// Indicates whether the current user holds admin rights on this Discord server.
    /// </summary>
    public bool IsAdmin { get; set; }
}
