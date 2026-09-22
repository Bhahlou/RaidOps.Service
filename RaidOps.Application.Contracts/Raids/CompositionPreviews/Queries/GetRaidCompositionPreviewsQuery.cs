using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;

/// <summary>
/// Returns every raid composition preview belonging to a guild branch. The requesting user must
/// hold <see cref="GuildAccessLevel.Officer"/> access on the guild branch.
/// </summary>
public class GetRaidCompositionPreviewsQuery : IQueryRequest<List<RaidCompositionPreviewSummaryResponse>>
{
    /// <summary>Discord snowflake ID of the guild whose previews to retrieve.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch whose previews to retrieve.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
