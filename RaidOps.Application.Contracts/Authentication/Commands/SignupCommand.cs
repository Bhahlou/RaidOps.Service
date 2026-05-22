using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Authentication.Commands;

/// <summary>
/// Command issued after a successful Discord OAuth2 callback to create or update
/// the user's account and synchronise their guild memberships.
/// </summary>
public class SignupCommand : ICommandRequest
{
    /// <summary>
    /// Gets or sets the Discord snowflake ID that uniquely identifies the user.
    /// </summary>
    public required string DiscordId { get; set; }

    /// <summary>
    /// Gets or sets the short-lived Discord OAuth2 access token used to query the Discord API.
    /// </summary>
    public required string DiscordAccessToken { get; set; }

    /// <summary>
    /// Gets or sets the long-lived Discord OAuth2 refresh token stored for future silent refreshes.
    /// </summary>
    public required string DiscordRefreshToken { get; set; }
}
