using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Events.Commands;

/// <summary>
/// Creates a standalone raid event, not tied to any recurring series. The requesting user must
/// hold <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class CreateAdhocRaidEventCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer creating this event. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this event targets. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Display name (e.g. "One-shot Kara clear").</summary>
    public required string Name { get; set; }

    /// <summary>UTC timestamp this event starts at.</summary>
    public required DateTime StartsAtUtc { get; set; }

    /// <summary>Number of groups in the raid grid.</summary>
    public required int GroupCount { get; set; }

    /// <summary>Number of slots per group in the raid grid.</summary>
    public required int SlotsPerGroup { get; set; }

    /// <summary>IDs of the raid zones this event targets. Must contain at least one zone.</summary>
    public required List<int> RaidZoneIds { get; set; }

    /// <summary>
    /// Surrogate ID of another raid event (in this same guild branch) whose lockout this one
    /// extends — e.g. a second night on the same Black Temple lock. <c>null</c> for a standalone
    /// event, the common case.
    /// </summary>
    public int? ExtendsRaidEventId { get; set; }

    /// <summary>
    /// Overrides the guild branch's default <see cref="SignupMode"/> for this one event, e.g. an
    /// exceptional Signup-mode raid on an otherwise DefaultPresent branch. <c>null</c> means "use
    /// the branch default," the common case.
    /// </summary>
    public SignupMode? SignupModeOverride { get; set; }

    /// <summary>
    /// Discord snowflake ID of a dedicated channel this event's raid-related notifications
    /// (published/composition/signup-call) should all post to instead of the guild-wide configured
    /// channel. <c>null</c> means "use the guild-wide configured channel," the common case.
    /// </summary>
    public string? DedicatedAnnouncementChannelId { get; set; }

    /// <summary>
    /// Whether <see cref="DedicatedAnnouncementChannelId"/> was just created by RaidOps for this
    /// event rather than an existing channel the officer picked — ignored when
    /// <see cref="DedicatedAnnouncementChannelId"/> is <c>null</c>. See
    /// <see cref="Domain.Models.Raids.RaidEvent.DedicatedAnnouncementChannelIsBotOwned"/>.
    /// </summary>
    public bool DedicatedAnnouncementChannelIsBotOwned { get; set; }
}
