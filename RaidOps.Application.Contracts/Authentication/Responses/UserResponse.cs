using RaidOps.Application.Contracts.Notifications.Responses;

namespace RaidOps.Application.Contracts.Authentication.Responses;

/// <summary>
/// Lightweight representation of an authenticated user's public profile,
/// returned by the <c>GET /me</c> endpoint.
/// </summary>
public class UserResponse
{
    /// <summary>
    /// Gets or sets the Discord snowflake ID that uniquely identifies the user.
    /// </summary>
    public required string DiscordId { get; set; }

    /// <summary>
    /// Gets or sets the user's current Discord display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the Discord avatar hash, or <c>null</c> if the user has no custom avatar.
    /// Use with the Discord CDN URL pattern to build the full avatar URL.
    /// </summary>
    public string? AvatarHash { get; set; }

    /// <summary>
    /// Gets or sets the list of Discord guilds the user belongs to,
    /// along with each guild's RaidOps registration status and the user's admin flag.
    /// </summary>
    public List<UserGuildResponse> Guilds { get; set; } = [];

    /// <summary>
    /// Gets or sets the derived in-app notifications currently active for this user
    /// (not already dismissed).
    /// </summary>
    public List<NotificationResponse> Notifications { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the front-end changelog entries the user has acknowledged.
    /// </summary>
    public List<string> SeenChangelogEntryIds { get; set; } = [];
}
