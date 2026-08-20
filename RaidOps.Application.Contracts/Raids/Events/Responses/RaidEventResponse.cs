using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Events.Responses;

/// <summary>A single raid event with its target zones and slot assignments.</summary>
public class RaidEventResponse
{
    /// <summary>Internal event ID.</summary>
    public required int Id { get; set; }

    /// <summary>FK to the series this occurrence was materialized from, or <c>null</c> for an ad-hoc event.</summary>
    public int? RaidSeriesId { get; set; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>FK to the WoW game-version branch this event targets, resolved via its guild branch.</summary>
    public required int BranchId { get; set; }

    /// <summary>Display name of the WoW game-version branch.</summary>
    public required string BranchName { get; set; }

    /// <summary>UTC timestamp this event starts at.</summary>
    public required DateTime StartsAtUtc { get; set; }

    /// <summary>Number of groups in the raid grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>How attendance is determined for this event.</summary>
    public required SignupMode SignupMode { get; set; }

    /// <summary>Lifecycle status of this event.</summary>
    public required RaidEventStatus Status { get; set; }

    /// <summary>Draft/published status of this event.</summary>
    public required RaidPublicationStatus PublicationStatus { get; set; }

    /// <summary>UTC timestamp this event was published at, or <c>null</c> while still a draft.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Discord snowflake ID of the officer who published this event, or <c>null</c> while still a draft.</summary>
    public string? PublishedByDiscordId { get; set; }

    /// <summary>The raid zones this event targets.</summary>
    public required List<RaidZoneRefResponse> RaidZones { get; set; }

    /// <summary>The sparse slot assignments for this event.</summary>
    public required List<RaidSlotAssignmentResponse> Assignments { get; set; }

    /// <summary>
    /// Discord IDs of every roster player (assigned or not) who is currently ineligible for
    /// assignment to this event, so the front end can show a drop target as blocked before the drag
    /// is even released. For <see cref="Enums.SignupMode.DefaultPresent"/> events this mirrors
    /// <c>AssignCharacterToSlotCommandHandler</c>'s absence check exactly (hard <c>Absent</c>, or
    /// <c>Partial</c> outside the event's start time); for <see cref="Enums.SignupMode.Signup"/>
    /// events it's every roster player without an <see cref="SignupStatus.Accepted"/>
    /// <c>RaidSignup</c>. Only one gate ever applies to a given event, since <see cref="SignupMode"/>
    /// is fixed at creation.
    /// </summary>
    public required List<string> IneligiblePlayerDiscordIds { get; set; }

    /// <summary>
    /// The requesting user's own response for this event, or <c>null</c> if the event isn't in
    /// <see cref="Enums.SignupMode.Signup"/> mode or they haven't responded yet.
    /// </summary>
    public SignupStatus? MySignupStatus { get; set; }

    /// <summary>The character behind <see cref="MySignupStatus"/>, set for <see cref="SignupStatus.Accepted"/> and <see cref="SignupStatus.Tentative"/>.</summary>
    public int? MySignupCharacterId { get; set; }

    /// <summary>The spec behind <see cref="MySignupStatus"/>, set for <see cref="SignupStatus.Accepted"/> and <see cref="SignupStatus.Tentative"/>.</summary>
    public int? MySignupSpecId { get; set; }

    /// <summary>
    /// For <see cref="Enums.SignupMode.Signup"/> events, maps each roster player's Discord ID to
    /// the character ID they're <see cref="SignupStatus.Accepted"/> with — lets the roster pool
    /// narrow a player's candidate characters down to the one they actually signed up with instead
    /// of every alt on their account. Empty for <see cref="Enums.SignupMode.DefaultPresent"/> events.
    /// </summary>
    public required Dictionary<string, int> AcceptedCharacterIdsByPlayerDiscordId { get; set; }

    /// <summary>
    /// Discord snowflake ID of this event's dedicated announcement channel, or <c>null</c> to use
    /// the guild-wide configured one. Lets the edit dialog pre-select the event's current channel.
    /// </summary>
    public string? DedicatedAnnouncementChannelId { get; set; }

    /// <summary>Whether <see cref="DedicatedAnnouncementChannelId"/> was created by RaidOps specifically for this event.</summary>
    public bool DedicatedAnnouncementChannelIsBotOwned { get; set; }
}
