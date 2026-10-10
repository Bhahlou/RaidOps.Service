using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Raids.Events.Commands;

/// <summary>
/// Posts a one-off "grouping up now" ping — mentioning every currently-assigned player and
/// instructing them to whisper the raid leader's character for an invite — in the branch's
/// configured composition-announcement channel. Distinct from the standing composition
/// announcement (which never pings, to avoid spamming on every roster edit): this is a single,
/// explicitly officer-triggered alert. The requesting user must hold
/// <see cref="Domain.Enums.GuildAccessLevel.Officer"/> access on <see cref="GuildId"/>.
/// </summary>
public class TriggerRaidGroupingCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this event belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Discord snowflake ID of the officer triggering the ping. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this event belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>ID of the published raid event to ping. Set by the controller from the route, not from the request body.</summary>
    public int EventId { get; set; }

    /// <summary>
    /// Name of the assigned character the ping should reference (players are told to whisper this
    /// character for an invite). Optional — when omitted, resolves to the requester's own assigned
    /// character in this event, failing with <see cref="Common.ResponseDetail.RaidGroupingRequesterHasNoCharacter"/>
    /// if they don't have one. When provided, must match an assigned character's name or the command
    /// fails with <see cref="Common.ResponseDetail.RaidGroupingCharacterNotFound"/>.
    /// </summary>
    public string? CharacterName { get; set; }
}
