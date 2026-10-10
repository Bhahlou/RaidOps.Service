using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A recurring raid template ("Split 1 — SSC/TK, Tuesdays 21:00") owned by a guild. Concrete
/// <see cref="RaidEvent"/> occurrences are materialized on demand (no background job) by an
/// idempotent command whenever the raid board is opened over a date range, rather than eagerly
/// on a schedule.
/// </summary>
[Table("RaidSeries")]
public class RaidSeries
{
    /// <summary>Auto-incremented primary key.</summary>
    [Key]
    public int Id { get; set; }

    /// <summary>Discord snowflake ID of the guild this series belongs to.</summary>
    [Required]
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Display name (e.g. "Split 1").</summary>
    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>FK to the guild branch this series targets (e.g. Classic Anniversary on this guild).</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Day of the week each occurrence falls on.</summary>
    public DayOfWeek RecurrenceDayOfWeek { get; set; }

    /// <summary>Start time of each occurrence, local to <see cref="Guild.Timezone"/>.</summary>
    public TimeOnly RecurrenceStartTimeLocal { get; set; }

    /// <summary>Number of weeks between occurrences. Defaults to 1 (weekly); 2 supports bi-weekly series.</summary>
    public int RecurrenceIntervalWeeks { get; set; } = 1;

    /// <summary>Number of groups in the raid grid, copied onto every materialized occurrence.</summary>
    public int GroupCount { get; set; }

    /// <summary>Number of slots per group, copied onto every materialized occurrence.</summary>
    public int SlotsPerGroup { get; set; }

    /// <summary>
    /// How attendance is determined for occurrences of this series. Defaults to
    /// <see cref="SignupMode.DefaultPresent"/> — the only mode implemented in this milestone.
    /// </summary>
    public SignupMode SignupMode { get; set; } = SignupMode.DefaultPresent;

    /// <summary>
    /// Discord snowflake ID of a dedicated channel this series' occurrences should post all their
    /// raid-related notifications (published/composition/signup-call) to, instead of whatever's
    /// configured guild-wide — copied onto each materialized <see cref="RaidEvent"/>. <c>null</c>
    /// means "use the guild-wide configured channel," the default for every series.
    /// </summary>
    public string? DedicatedAnnouncementChannelId { get; set; }

    /// <summary>
    /// Discord snowflake ID of a category — when set (instead of, never alongside,
    /// <see cref="DedicatedAnnouncementChannelId"/>), each materialized occurrence gets its own
    /// fresh channel created in this category at materialization time (named after the raid and
    /// that occurrence's own date), rather than every occurrence sharing one fixed channel. Lets an
    /// officer get a distinct, recognizable channel per raid instead of one channel accumulating
    /// every week's chatter. <c>null</c> means "no per-occurrence channel," the default.
    /// </summary>
    public string? DedicatedAnnouncementChannelCategoryId { get; set; }

    /// <summary>
    /// Whether this series is still active. Deactivating a series stops future materialization
    /// but never deletes or alters the events it already produced.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Discord snowflake ID of the officer who created this series.</summary>
    [Required]
    public string CreatedByDiscordId { get; set; } = string.Empty;

    /// <summary>UTC timestamp of when this series was created.</summary>
    public DateTime CreatedAt { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this series belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The guild branch this series targets.</summary>
    public virtual GuildBranch GuildBranch { get; set; } = null!;

    /// <summary>The set of raid zones every materialized occurrence targets by default.</summary>
    public virtual ICollection<RaidSeriesZone> DefaultZones { get; set; } = [];

    /// <summary>The concrete occurrences materialized from this series.</summary>
    public virtual ICollection<RaidEvent> Events { get; set; } = [];
}
