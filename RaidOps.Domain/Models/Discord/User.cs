using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// A RaidOps user, identified by their Discord account.
/// </summary>
[Table("Users")]
public class User
{
    /// <summary>Discord snowflake ID — primary key.</summary>
    [Key]
    public string DiscordId { get; set; } = string.Empty;

    /// <summary>Discord display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Discord avatar hash, or <c>null</c> if the user has no custom avatar.</summary>
    public string? AvatarHash { get; set; }

    /// <summary>Latest Discord OAuth2 refresh token, used to re-authenticate without user interaction.</summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last successful token refresh / sync.</summary>
    [Required]
    public DateTimeOffset LastRefresh { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>Discord guild memberships for this user.</summary>
    public virtual ICollection<UserGuild> UserGuilds { get; set; } = [];
}
