using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Calendar;

/// <summary>
/// A one-off availability declaration for a single date or date range. Scope is either Global
/// (<see cref="GuildId"/> and <see cref="GuildBranchId"/> both null — applies everywhere the
/// member has an active character) or a specific <see cref="Discord.GuildBranch"/> (both set) —
/// no intermediate "whole guild" scope. When it overlaps a date also covered by a
/// <see cref="RecurringAvailabilityPattern"/>, this always takes precedence.
/// </summary>
[Table("AvailabilityExceptions")]
public class AvailabilityDeclaration
{
    /// <summary>Auto-incremented primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the member who made this declaration.</summary>
    [Required]
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the guild this declaration is scoped to, or <c>null</c> for a Global declaration. Set together with <see cref="GuildBranchId"/>.</summary>
    public string? GuildId { get; set; }

    /// <summary>FK to the specific branch this declaration is scoped to, or <c>null</c> for a Global declaration. Set together with <see cref="GuildId"/>.</summary>
    public int? GuildBranchId { get; set; }

    /// <summary>First date covered by this declaration (inclusive). Equal to <see cref="EndDate"/> for a single day.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last date covered by this declaration (inclusive). Equal to <see cref="StartDate"/> for a single day.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Declared status for every date in the range.</summary>
    public DayAvailabilityStatus Status { get; set; }

    /// <summary>Optional free-text reason (e.g. "vacances", "rendez-vous médecin").</summary>
    [MaxLength(256)]
    public string? Reason { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time from which the member becomes available (e.g. arriving late). Null if not bounded on this side.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    /// <summary>When <see cref="Status"/> is <see cref="DayAvailabilityStatus.Partial"/>, the time until which the member remains available (e.g. leaving early for a night shift). Null if not bounded on this side.</summary>
    public TimeOnly? AvailableUntil { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The member who made this declaration.</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>The guild this declaration is scoped to, or <c>null</c> for a Global declaration.</summary>
    public virtual Guild? Guild { get; set; }

    /// <summary>The specific branch this declaration is scoped to, or <c>null</c> for a Global declaration.</summary>
    public virtual GuildBranch? GuildBranch { get; set; }
}
