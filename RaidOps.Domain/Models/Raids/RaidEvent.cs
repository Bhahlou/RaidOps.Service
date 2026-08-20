using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

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

    /// <summary>FK to the guild branch this event targets.</summary>
    public int GuildBranchId { get; set; }

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

    /// <summary>
    /// Discord snowflake ID of the channel the standing "current composition" announcement embed
    /// was posted in, or <c>null</c> if it's never been posted (composition announcements are
    /// disabled for this guild/branch, or the event isn't published yet).
    /// </summary>
    public string? CompositionAnnouncementChannelId { get; set; }

    /// <summary>
    /// Discord snowflake ID of the standing "current composition" announcement message — edited in
    /// place as the roster changes, rather than reposted. <c>null</c> alongside
    /// <see cref="CompositionAnnouncementChannelId"/>.
    /// </summary>
    public string? CompositionAnnouncementMessageId { get; set; }

    /// <summary>
    /// Discord snowflake ID of the channel the standing signup-call embed (Accept/Tentative/Decline
    /// buttons) was posted in, for <see cref="SignupMode.Signup"/> events. <c>null</c> if it's never
    /// been posted (signup-call announcements are disabled for this guild/branch, the event isn't
    /// published yet, or its mode isn't <see cref="SignupMode.Signup"/>).
    /// </summary>
    public string? SignupCallAnnouncementChannelId { get; set; }

    /// <summary>
    /// Discord snowflake ID of the standing signup-call announcement message — edited in place as
    /// responses come in, rather than reposted. <c>null</c> alongside
    /// <see cref="SignupCallAnnouncementChannelId"/>.
    /// </summary>
    public string? SignupCallAnnouncementMessageId { get; set; }

    /// <summary>
    /// Discord snowflake ID of a dedicated channel this event's raid-related notifications
    /// (published/composition/signup-call) should all post to instead of whatever's configured
    /// guild-wide — officer-chosen at creation time (or copied from the originating
    /// <see cref="RaidSeries"/>). <c>null</c> means "use the guild-wide configured channel," the
    /// default. Distinct from <see cref="CompositionAnnouncementChannelId"/>/
    /// <see cref="SignupCallAnnouncementChannelId"/>, which only ever cache where a message ended
    /// up, never drive the choice of channel.
    /// </summary>
    public string? DedicatedAnnouncementChannelId { get; set; }

    /// <summary>
    /// Whether <see cref="DedicatedAnnouncementChannelId"/> was created by RaidOps specifically for
    /// this event (the create dialog's "new channel" path) rather than an existing channel the
    /// officer picked. Drives whether deleting this event also deletes the Discord channel —
    /// deleting an officer-picked existing channel would be destructive well beyond this raid.
    /// Always <c>false</c> when <see cref="DedicatedAnnouncementChannelId"/> is <c>null</c>.
    /// </summary>
    public bool DedicatedAnnouncementChannelIsBotOwned { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The guild this event belongs to.</summary>
    public virtual Guild Guild { get; set; } = null!;

    /// <summary>The series this occurrence was materialized from, or <c>null</c> for an ad-hoc event.</summary>
    public virtual RaidSeries? RaidSeries { get; set; }

    /// <summary>The guild branch this event targets.</summary>
    public virtual GuildBranch GuildBranch { get; set; } = null!;

    /// <summary>The set of raid zones this event targets.</summary>
    public virtual ICollection<RaidEventZone> TargetZones { get; set; } = [];

    /// <summary>The sparse slot assignments for this event.</summary>
    public virtual ICollection<RaidSlotAssignment> Assignments { get; set; } = [];

    /// <summary>Member responses for this event, only meaningful when <see cref="SignupMode"/> is <see cref="SignupMode.Signup"/>.</summary>
    public virtual ICollection<RaidSignup> Signups { get; set; } = [];
}
