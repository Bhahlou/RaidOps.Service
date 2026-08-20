using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using WowCharacter = RaidOps.Domain.Models.Character.Character;

namespace RaidOps.Domain.Models.Raids;

/// <summary>
/// A single member's self-declared response to a <see cref="RaidEvent"/> whose
/// <see cref="RaidEvent.SignupMode"/> is <see cref="SignupMode.Signup"/>. Composite primary key:
/// (<see cref="RaidEventId"/>, <see cref="UserDiscordId"/>) — a member has at most one live
/// response per event, so re-responding is always an upsert of the same row. Keyed on the player
/// rather than a specific <see cref="Character.Character"/>, since a member RSVPs once per event
/// regardless of which character ends up assigned to a slot.
/// </summary>
[Table("RaidSignups")]
public class RaidSignup
{
    /// <summary>FK to the event this response is for.</summary>
    public int RaidEventId { get; set; }

    /// <summary>Discord snowflake ID of the member who responded.</summary>
    [Required]
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>The member's current response.</summary>
    public SignupStatus Status { get; set; }

    /// <summary>
    /// The character this member is bringing, required when <see cref="Status"/> is
    /// <see cref="SignupStatus.Accepted"/> and always <c>null</c> otherwise — a Tentative/Declined
    /// response doesn't commit a character. Slot-assignment eligibility requires an exact match on
    /// this character, not just any of the player's characters.
    /// </summary>
    public int? CharacterId { get; set; }

    /// <summary>
    /// The spec <see cref="CharacterId"/> is signing up as, required alongside it when
    /// <see cref="Status"/> is <see cref="SignupStatus.Accepted"/> and always <c>null</c> otherwise —
    /// same lifecycle as <see cref="CharacterId"/>, just one level more specific.
    /// </summary>
    public int? SpecId { get; set; }

    /// <summary>UTC timestamp of when this response was last set.</summary>
    public DateTime RespondedAtUtc { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    /// <summary>The event this response is for.</summary>
    public virtual RaidEvent RaidEvent { get; set; } = null!;

    /// <summary>The member who responded.</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>The character this member is bringing, when <see cref="CharacterId"/> is set.</summary>
    public virtual WowCharacter? Character { get; set; }

    /// <summary>The spec being brought, when <see cref="SpecId"/> is set.</summary>
    public virtual Spec? Spec { get; set; }
}
