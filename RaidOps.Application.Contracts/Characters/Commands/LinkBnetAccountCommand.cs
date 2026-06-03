using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Characters.Commands;

/// <summary>
/// Command that links (or re-links) a Battle.net account to a RaidOps user.
/// Called after the BNet OAuth2 callback has successfully exchanged the code for tokens.
/// </summary>
public class LinkBnetAccountCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the RaidOps user linking their BNet account.</summary>
    public required string UserDiscordId { get; set; }

    /// <summary>Blizzard's numeric account ID (from BNet userinfo endpoint).</summary>
    public required string BnetId { get; set; }

    /// <summary>The user's BattleTag (e.g. "Username#1234").</summary>
    public required string BattleTag { get; set; }

    /// <summary>BNet OAuth2 access token.</summary>
    public required string AccessToken { get; set; }

    /// <summary>BNet OAuth2 refresh token (may be null).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>UTC expiry of the access token.</summary>
    public required DateTimeOffset TokenExpiry { get; set; }

    /// <summary>BNet region: "us", "eu", "kr", or "tw".</summary>
    public required string Region { get; set; }
}
