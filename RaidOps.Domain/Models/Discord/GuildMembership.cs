using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using WowCharacter = RaidOps.Domain.Models.Character.Character;

namespace RaidOps.Domain.Models.Discord;

/// <summary>
/// Pivot table linking a <see cref="Character"/> to a RaidOps-registered <see cref="Guild"/>.
/// A character may belong to at most one guild roster at a time (composite PK enforces this).
/// The owning player's in-guild rank is tracked separately on <see cref="UserGuild.PlayerRank"/>.
/// </summary>
[Table("GuildMemberships")]
public class GuildMembership
{
    /// <summary>FK to the character on the roster.</summary>
    public int CharacterId { get; set; }

    /// <summary>Discord snowflake ID of the guild.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Raid-composition rank of this character (Main / Split / Alt).</summary>
    public CharacterRank CharacterRank { get; set; } = CharacterRank.Main;

    /// <summary>UTC timestamp of when the character joined the roster.</summary>
    public DateTime JoinedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The character on the roster.</summary>
    public virtual WowCharacter Character { get; set; } = null!;

    /// <summary>The guild this membership belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;
}