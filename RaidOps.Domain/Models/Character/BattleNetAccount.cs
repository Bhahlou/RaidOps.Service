using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Character;

/// <summary>
/// A Battle.net account linked to a RaidOps user.
/// A user may link several BNet accounts (composite key: <see cref="UserDiscordId"/> +
/// <see cref="BnetId"/>). Stores the OAuth2 tokens needed to call the BNet API on the user's
/// behalf for that specific account.
/// </summary>
[Table("BattleNetAccounts")]
public class BattleNetAccount
{
    /// <summary>
    /// Discord ID of the linked RaidOps user. First half of the composite primary key
    /// (see <see cref="RaidOpsDbContext.OnModelCreating"/>), and foreign key to <see cref="User"/>.
    /// </summary>
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>
    /// Blizzard's numeric account ID (from BNet token introspection).
    /// Second half of the composite primary key — distinguishes accounts of the same user.
    /// </summary>
    [Required, MaxLength(32)]
    public string BnetId { get; set; } = string.Empty;

    /// <summary>The Battle.net BattleTag (e.g. "Bhahlou#1234").</summary>
    [Required, MaxLength(64)]
    public string BattleTag { get; set; } = string.Empty;

    /// <summary>Current OAuth2 access token for BNet API calls.</summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>OAuth2 refresh token (may be absent depending on BNet OAuth flow).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>UTC expiry of the current access token.</summary>
    public DateTimeOffset TokenExpiry { get; set; }

    /// <summary>
    /// BNet region this account belongs to: "us", "eu", "kr", or "tw".
    /// All API calls for this user must target this region's base URL.
    /// </summary>
    [Required, MaxLength(4)]
    public string Region { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The RaidOps user who linked this BNet account.</summary>
    public virtual User User { get; set; } = null!;
}
