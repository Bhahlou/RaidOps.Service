using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models;

[Table("Guilds")]
public class Guild
{
    /// <summary>
    /// The Discord ID of the guild.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? IconHash { get; set; }

    /// <summary>
    /// Indicates whether this guild has been registered and configured in RaidOps.
    /// Only registered guilds appear in the guild selector and unlock full features.
    /// </summary>
    public bool IsRegistered { get; set; }

    public virtual ICollection<UserGuild> UserGuilds { get; set; } = [];
}
