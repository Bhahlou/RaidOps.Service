using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;

/// <summary>
/// Creates a new named raid composition preview, with an empty grid sized from
/// <see cref="GroupCount"/> x 5 (the constant WoW group size). The requesting user must hold
/// <see cref="GuildAccessLevel.Officer"/> access on the guild branch.
/// </summary>
public class CreateRaidCompositionPreviewCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this preview belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this preview belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the officer creating this preview. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Display name (e.g. "40-man target comp").</summary>
    public required string Name { get; set; }

    /// <summary>Number of groups in the grid — must be between 1 and 8.</summary>
    public required int GroupCount { get; set; }
}
