using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.Signups.Commands;

/// <summary>
/// Sets the requesting member's own Accepted/Tentative/Declined response to a
/// <see cref="Domain.Models.Raids.RaidEvent"/> in <see cref="SignupMode.Signup"/> mode — a single
/// idempotent upsert, since (EventId, RequesterDiscordId) unambiguously identifies at most one
/// response. The requesting user must hold at least <see cref="Domain.Enums.GuildAccessLevel.Roster"/>
/// access on <see cref="GuildId"/>, and the event must be published.
/// </summary>
public class SetMyRaidSignupCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this event belongs to. Set by the controller/bot, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the member responding. Set by the controller/bot, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this event belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>ID of the raid event being responded to. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }

    /// <summary>The member's response.</summary>
    public required SignupStatus Status { get; set; }

    /// <summary>
    /// The character the member is bringing — required when <see cref="Status"/> is
    /// <see cref="SignupStatus.Accepted"/> or <see cref="SignupStatus.Tentative"/>, ignored
    /// (always stored as <c>null</c>) for <see cref="SignupStatus.Declined"/>.
    /// </summary>
    public int? CharacterId { get; set; }

    /// <summary>
    /// The spec <see cref="CharacterId"/> is signing up as — required alongside it when
    /// <see cref="Status"/> is <see cref="SignupStatus.Accepted"/> or
    /// <see cref="SignupStatus.Tentative"/>, ignored (always stored as <c>null</c>) for
    /// <see cref="SignupStatus.Declined"/>.
    /// </summary>
    public int? SpecId { get; set; }
}
