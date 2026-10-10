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

    /// <summary>
    /// FK to the specific <see cref="GuildBranch"/> this membership was joined on. Populated once
    /// at join time from the guild branch matching the character's <see cref="WowCharacter.BranchId"/>,
    /// never updated afterwards (a character's branch never changes after creation). Indexed
    /// separately from <see cref="GuildId"/> since "all roster members of guild X on branch Y" is
    /// the dominant roster query.
    /// </summary>
    public int GuildBranchId { get; set; }

    /// <summary>Raid-composition rank of this character (Main / Split / Alt).</summary>
    public CharacterRank CharacterRank { get; set; } = CharacterRank.Main;

    /// <summary>UTC timestamp of when the character joined the roster.</summary>
    public DateTime JoinedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The character on the roster.</summary>
    public virtual WowCharacter Character { get; set; } = null!;

    /// <summary>The guild this membership belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The specific guild branch this membership was joined on.</summary>
    public virtual GuildBranch GuildBranch { get; set; } = null!;
}