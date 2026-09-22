using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Raids.Attributions;

/// <summary>
/// A single row of a guild's evolving raid-attribution template — a reusable "thing that always
/// needs assigning" (a buff, a curse, a tank swap, an interrupt rotation), independent of any
/// specific <see cref="RaidEvent"/>. Officers add/edit/reorder these as their raid content
/// changes; each row is later filled in per event via <see cref="RaidEventAttribution"/>.
/// Guild-wide only (no per-branch override) — unlike <c>GuildNotificationSetting</c>, this isn't
/// branch-specific config. <see cref="RaidBossId"/> <c>null</c> means a "General" row (shown on
/// every raid event regardless of boss, the original shape of this template); non-null scopes the
/// row to one specific boss encounter, only shown/fillable on events targeting that boss's zone.
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

    /// <summary>
    /// What icon-related field to render for this row's section header, when <see cref="Section"/>
    /// is set — deliberately independent of any cell icon on the rows themselves (e.g. the section
    /// header shows the curse to maintain, while a row's own cell shows the spell to actually cast).
    /// Set once per (guild, <see cref="RaidBossId"/>, <see cref="Section"/>) via
    /// <c>SetAttributionSectionIconCommand</c>, which writes it to every row sharing that section —
    /// denormalized on each row rather than a separate Section entity, same trade-off as <see cref="Section"/>
    /// itself. <see cref="AttributionIconSource.None"/> (the default) shows no section icon.
    /// </summary>
    public AttributionIconSource SectionIconSource { get; set; } = AttributionIconSource.None;

    /// <summary>FK to the section header's linked spell, set only when <see cref="SectionIconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public int? SectionSpellId { get; set; }

    /// <summary>The section header's raid target marker, set only when <see cref="SectionIconSource"/> is <see cref="AttributionIconSource.RaidMarker"/>.</summary>
    public RaidMarkerIcon? SectionRaidMarker { get; set; }

    /// <summary>The section header's built-in role icon, set only when <see cref="SectionIconSource"/> is <see cref="AttributionIconSource.StaticRole"/>.</summary>
    public SpecRole? SectionStaticRole { get; set; }

    /// <summary>Display ordering within the guild's template.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this row can have a variable number of instances per raid event (e.g. "Innervate" —
    /// however many druids show up that night), instead of always exactly one. Instance count is
    /// decided per event on the Assignments tab, not here — see <see cref="RaidEventAttribution.InstanceIndex"/>.
    /// </summary>
    public bool IsRepeatable { get; set; }

    /// <summary>
    /// FK to the specific boss this row is scoped to, or <c>null</c> for a "General" row shown on
    /// every raid event. Non-null rows are only shown/fillable on events targeting that boss's
    /// <see cref="RaidZone"/> (see <see cref="RaidEventZone"/>).
    /// </summary>
    public int? RaidBossId { get; set; }

    /// <summary>UTC timestamp this row was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who created this row.</summary>
    [Required]
    public string CreatedByDiscordId { get; set; } = string.Empty;

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this definition belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The boss this row is scoped to, or <c>null</c> for a "General" row.</summary>
    public virtual RaidBoss? RaidBoss { get; set; }

    /// <summary>The section header's linked spell, or <c>null</c> unless <see cref="SectionIconSource"/> is <see cref="AttributionIconSource.Spell"/>.</summary>
    public virtual Spell? SectionSpell { get; set; }

    /// <summary>This row's ordered cells (icons and name slots) — see <see cref="AttributionDefinitionCell"/>. Per-event fills are reached through a cell, not this row directly.</summary>
    public virtual ICollection<AttributionDefinitionCell> Cells { get; set; } = [];
}
