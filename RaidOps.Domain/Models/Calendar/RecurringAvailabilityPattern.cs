using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Calendar;

/// <summary>
/// A recurring availability pattern, defined as a fixed-length cycle of days anchored to a
/// reference date. Scope is either Global (<see cref="GuildId"/> and <see cref="GuildBranchId"/>
/// both null — applies everywhere the member has an active character) or a specific
/// <see cref="Discord.GuildBranch"/> (both set) — no intermediate "whole guild" scope. Covers both
/// simple weekly recurrence (<see cref="CycleLengthDays"/> = 7) and arbitrary shift rotations
/// (e.g. a 5x8 work schedule) with the same mechanism — <see cref="RecurringAvailabilityPatternDay"/>
/// rows mark which offsets in the cycle are not fully available. A one-off
/// <see cref="AvailabilityDeclaration"/> on a given date always takes precedence over this pattern.
///
/// Each row is an immutable version valid over <see cref="EffectiveFrom"/>..<see cref="EffectiveUntil"/>
/// — editing or stopping a pattern never mutates a version that has already applied to a past date;
/// it closes this row's <see cref="EffectiveUntil"/> instead (and, for edits, inserts a new row
/// effective from today). This is what keeps past resolved days stable when a member later changes
/// or removes their recurring pattern.
/// </summary>
[Table("RecurringAvailabilityPatterns")]
public class RecurringAvailabilityPattern
{
    /// <summary>Auto-incremented primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the member this pattern belongs to.</summary>
    [Required]
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the guild this pattern is scoped to, or <c>null</c> for a Global pattern. Set together with <see cref="GuildBranchId"/>.</summary>
    public string? GuildId { get; set; }

    /// <summary>FK to the specific branch this pattern is scoped to, or <c>null</c> for a Global pattern. Set together with <see cref="GuildId"/>.</summary>
    public int? GuildBranchId { get; set; }

    /// <summary>Optional friendly name for the member's own reference (e.g. "Rotation 5x8").</summary>
    [MaxLength(128)]
    public string? Label { get; set; }

    /// <summary>Length of the recurrence cycle in days (7 for a weekly pattern, or any other length for a shift rotation).</summary>
    public int CycleLengthDays { get; set; }

    /// <summary>Reference date at which offset 0 of the cycle begins.</summary>
    public DateOnly AnchorDate { get; set; }

    /// <summary>First date (inclusive) this version applies from.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Last date (inclusive) this version applies until, or <c>null</c> if it's the current, still-open version.</summary>
    public DateOnly? EffectiveUntil { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The member this pattern belongs to.</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>The guild this pattern is scoped to, or <c>null</c> for a Global pattern.</summary>
    public virtual Guild? Guild { get; set; }

    /// <summary>The specific branch this pattern is scoped to, or <c>null</c> for a Global pattern.</summary>
    public virtual GuildBranch? GuildBranch { get; set; }

    /// <summary>The days within the cycle that are not fully available. A missing offset means fully available.</summary>
    public virtual ICollection<RecurringAvailabilityPatternDay> Days { get; set; } = [];
}
