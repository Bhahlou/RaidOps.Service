using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Raids.Attributions;

/// <summary>
/// A single row of a guild's evolving raid-attribution template — a reusable "thing that always
/// needs assigning" (a buff, a curse, a tank swap, an interrupt rotation), independent of any
/// specific <see cref="RaidEvent"/>. Officers add/edit/reorder these as their raid content
/// changes; each row is later filled in per event via <see cref="RaidEventAttribution"/>.
/// Guild-wide only (no per-branch override) — unlike <c>GuildNotificationSetting</c>, this isn't
/// branch-specific config.
/// </summary>
[Table("GuildAttributionDefinitions")]
public class GuildAttributionDefinition
{
    /// <summary>Surrogate primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the guild this definition belongs to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>
    /// Display label — pre-filled from the linked spell's localized name when one is picked, but
    /// always independently editable (a custom short label like "MT" is common).
    /// </summary>
    [Required, MaxLength(64)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Free-text grouping label (e.g. "Tanks &amp; Heals", "Buffs", "Curses"). Rows are grouped by
    /// consecutive matching <see cref="Section"/> within <see cref="SortOrder"/> for display — no
    /// separate Section entity, the guild just retypes the label on however many rows it groups.
    /// <c>null</c> for an ungrouped row.
    /// </summary>
    [MaxLength(64)]
    public string? Section { get; set; }

    /// <summary>Display ordering within the guild's template.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this row can have a variable number of instances per raid event (e.g. "Innervate" —
    /// however many druids show up that night), instead of always exactly one. Instance count is
    /// decided per event on the Assignments tab, not here — see <see cref="RaidEventAttribution.InstanceIndex"/>.
    /// </summary>
    public bool IsRepeatable { get; set; }

    /// <summary>UTC timestamp this row was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who created this row.</summary>
    [Required]
    public string CreatedByDiscordId { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this definition belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>This row's ordered cells (icons and name slots) — see <see cref="AttributionDefinitionCell"/>. Per-event fills are reached through a cell, not this row directly.</summary>
    public virtual ICollection<AttributionDefinitionCell> Cells { get; set; } = [];
}
