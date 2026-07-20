using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Calendar;

/// <summary>
/// A one-off availability declaration for a single date or date range, scoped to a member's
/// participation in a specific guild. When it overlaps a date also covered by a
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

    /// <summary>Discord snowflake ID of the guild this declaration applies to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

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

    /// <summary>The guild this declaration applies to.</summary>
    public virtual Guild Guild { get; set; } = null!;
}
