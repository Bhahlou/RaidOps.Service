using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models;

[Table("Users")]
public class User
{
    [Key]
    public string DiscordId { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? AvatarHash { get; set; }

    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset LastRefresh { get; set; }

    public virtual ICollection<UserGuild> UserGuilds { get; set; } = [];
}
