using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;

/// <summary>
/// Returns a single raid composition preview with its full slot grid — backs the composer page.
/// The requesting user must hold <see cref="GuildAccessLevel.Officer"/> access on the guild branch.
/// </summary>
public class GetRaidCompositionPreviewQuery : IQueryRequest<RaidCompositionPreviewResponse>
{
    /// <summary>Discord snowflake ID of the guild the preview belongs to.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch the preview belongs to.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>Surrogate ID of the preview to retrieve.</summary>
    public required int PreviewId { get; set; }
}
