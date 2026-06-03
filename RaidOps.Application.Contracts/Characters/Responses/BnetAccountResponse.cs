namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Represents the Battle.net account linked to the requesting user.
/// </summary>
public class BnetAccountResponse
{
    /// <summary>Gets or sets the Battle.net account ID.</summary>
    public required string BnetId { get; set; }

    /// <summary>Gets or sets the user's BattleTag (e.g. "Player#1234").</summary>
    public required string BattleTag { get; set; }

    /// <summary>Gets or sets the WoW region the account is linked to (e.g. "eu", "us").</summary>
    public required string Region { get; set; }

    /// <summary>Gets or sets the access token expiry timestamp (UTC).</summary>
    public required DateTimeOffset TokenExpiry { get; set; }
}
