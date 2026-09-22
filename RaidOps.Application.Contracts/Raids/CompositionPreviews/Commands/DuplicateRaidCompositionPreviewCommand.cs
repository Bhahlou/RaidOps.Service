using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;

/// <summary>
/// Clones an existing raid composition preview — its format/grid shape and every slot — under a
/// new name. The requesting user must hold <see cref="GuildAccessLevel.Officer"/> access on the
/// guild branch.
/// </summary>
public class DuplicateRaidCompositionPreviewCommand : ICommandRequest
{
    /// <summary>Discord snowflake ID of the guild this preview belongs to. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the guild branch this preview belongs to. Set by the controller from the route, not from the request body.</summary>
    public int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting officer. Set by the controller, not from the request body.</summary>
    public string RequesterDiscordId { get; set; } = string.Empty;

    /// <summary>Surrogate ID of the preview to duplicate. Set by the controller from the route, not from the request body.</summary>
    public int PreviewId { get; set; }

    /// <summary>Display name for the new, cloned preview.</summary>
    public required string NewName { get; set; }
}
