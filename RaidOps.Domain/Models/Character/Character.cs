using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Character;

/// <summary>
/// A WoW character imported from a user's Battle.net account.
/// One user can own many characters spread across different realms and branches.
/// </summary>
[Table("Characters")]
public class Character
{
    /// <summary>Internal auto-incremented identifier.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Character name as returned by the BNet API (e.g. "Arthas").</summary>
    [Required, MaxLength(32)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Character faction as resolved from the BNet API.
    /// Stored explicitly because some races (Pandaren, Dracthyr, Earthen) can be either faction.
    /// </summary>
    public Faction Faction { get; set; }

    /// <summary>Character gender as returned by the BNet API.</summary>
    public Gender Gender { get; set; }

    /// <summary>Blizzard's internal character ID, unique within a realm.</summary>
    public long BnetCharacterId { get; set; }

    /// <summary>Discord ID of the RaidOps user who owns this character.</summary>
    [Required]
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="BattleNetAccount.BnetId"/> of the linked BNet account this character was
    /// synced from, or <c>null</c> if it predates account tracking.
    /// This character is hard-deleted (cascade) when its source account is unlinked.
    /// </summary>
    [MaxLength(32)]
    public string? SourceBnetId { get; set; }

    /// <summary>FK to the branch this character belongs to.</summary>
    public int BranchId { get; set; }

    /// <summary>FK to the realm this character lives on.</summary>
    public int RealmId { get; set; }

    /// <summary>FK to the character's race.</summary>
    public int RaceId { get; set; }

    /// <summary>FK to the character's class.</summary>
    public int ClassId { get; set; }

    /// <summary>
    /// Whether this character has been explicitly selected by the user for use in RaidOps
    /// (roster, loot, calendar). Synced characters start as <c>false</c> until the user imports them.
    /// </summary>
    public bool IsActiveInRaidOps { get; set; }

    /// <summary>
    /// Avatar image URL from the BNet character-media endpoint.
    /// Populated on activation; <c>null</c> if the API call failed or has not run yet.
    /// </summary>
    [MaxLength(512)]
    public string? AvatarUrl { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The RaidOps user who owns this character.</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>The branch this character was synced from.</summary>
    public virtual Branch Branch { get; set; } = null!;

    /// <summary>The realm this character lives on.</summary>
    public virtual Realm Realm { get; set; } = null!;

    /// <summary>The character's race.</summary>
    public virtual Race Race { get; set; } = null!;

    /// <summary>The character's class.</summary>
    public virtual WowClass Class { get; set; } = null!;

    /// <summary>Per-expansion progress snapshots (level, item level, active specs).</summary>
    public virtual ICollection<CharacterExpansionState> ExpansionStates { get; set; } = [];

    /// <summary>User-curated specs this character is viable to raid with. Not tied to BNet sync.</summary>
    public virtual ICollection<CharacterRaidSpec> RaidSpecs { get; set; } = [];

    /// <summary>Guild rosters this character is currently a member of.</summary>
    public virtual ICollection<GuildMembership> GuildMemberships { get; set; } = [];
}
