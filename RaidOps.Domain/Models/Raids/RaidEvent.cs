using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A concrete raid occurrence, either materialized from a <see cref="RaidSeries"/> or created
/// ad-hoc. Targets a set of <see cref="RaidZone"/>s (via <see cref="TargetZones"/>) sharing a single
/// group/slot grid — e.g. a "Split SSC/TK" event covers both zones at once, and the lockout is
/// consumed for each of them independently.
/// </summary>
[Table("RaidEvents")]
public class RaidEvent
{
    /// <summary>Auto-incremented primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the guild this event belongs to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>FK to the series this occurrence was materialized from, or <c>null</c> for an ad-hoc event.</summary>
    public int? RaidSeriesId { get; set; }

    /// <summary>Display name (e.g. "Split 1").</summary>
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>FK to the game branch this event targets.</summary>
    public int BranchId { get; set; }

    /// <summary>UTC timestamp this occurrence starts at.</summary>
    public DateTime StartsAtUtc { get; set; }

    /// <summary>Number of groups in the raid grid.</summary>
    public int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public int SlotsPerGroup { get; set; }

    /// <summary>How attendance is determined for this occurrence.</summary>
    public SignupMode SignupMode { get; set; } = SignupMode.DefaultPresent;

    /// <summary>Lifecycle status of this occurrence.</summary>
    public RaidEventStatus Status { get; set; } = RaidEventStatus.Scheduled;

    /// <summary>
    /// Draft/published status of this event, orthogonal to <see cref="Status"/>. Defaults to
    /// <see cref="RaidPublicationStatus.Draft"/> — a raid stays private to officers until explicitly
    /// published via <c>PublishRaidEventCommand</c>.
    /// </summary>
    public RaidPublicationStatus PublicationStatus { get; set; } = RaidPublicationStatus.Draft;

    /// <summary>UTC timestamp this event was published at, or <c>null</c> while still a draft.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who published this event, or <c>null</c> while still a draft.</summary>
    public string? PublishedByDiscordId { get; set; }

    /// <summary>Discord snowflake ID of the user who created this event (or triggered its materialization).</summary>
    [Required]
    public string CreatedByDiscordId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when this event was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this event belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The series this occurrence was materialized from, or <c>null</c> for an ad-hoc event.</summary>
    public virtual RaidSeries? RaidSeries { get; set; }

    /// <summary>The game branch this event targets.</summary>
    public virtual Branch Branch { get; set; } = null!;

    /// <summary>The set of raid zones this event targets.</summary>
    public virtual ICollection<RaidEventZone> TargetZones { get; set; } = [];

    /// <summary>The sparse slot assignments for this event.</summary>
    public virtual ICollection<RaidSlotAssignment> Assignments { get; set; } = [];
}
