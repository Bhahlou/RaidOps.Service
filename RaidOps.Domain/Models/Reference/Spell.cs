using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaidOps.Domain.Models.Reference;

/// <summary>
/// A WoW spell/ability, identified by Blizzard's own spell ID (same convention as
/// <see cref="Spec"/>/<see cref="WowClass"/>). Global across every expansion/branch and deliberately
/// content-free: Blizzard spell IDs are reused/cumulative across the whole game, and the same ID can
/// carry a different name or icon on different branches (e.g. ID 1022 is "Hand of Protection" on
/// Classic but "Blessing of Protection" on Retail). What a spell is actually called and looks like
/// therefore lives per expansion in <see cref="SpellAvailability"/>. Populated by the periodic
/// wago.tools sync (see <c>SyncSpellsCommandHandler</c>) through an idempotent upsert.
/// </summary>
[Table("Spells")]
public class Spell
{
    /// <summary>Blizzard's spell ID. Assigned at import time; never auto-incremented.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The expansions this spell has been observed on, each with its own name/icon.</summary>
    public virtual ICollection<SpellAvailability> Availabilities { get; set; } = [];
}
