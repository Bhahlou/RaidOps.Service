using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models;

/// <summary>
/// Junction table between User and Guild.
/// </summary>
[Table("UserGuilds")]
public class UserGuild
{
    public string UserDiscordId { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    [ForeignKey(nameof(UserDiscordId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(GuildId))]
    public virtual Guild Guild { get; set; } = null!;
}
