using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Events.Responses;

namespace RaidOps.Application.Contracts.Raids.Events.Queries;

/// <summary>
/// Returns a single raid event's minimal identity (id + name) — backs the raid detail page's
/// breadcrumb, which only needs the name and shouldn't have to load the full board for it. The
/// requesting user must hold at least <see cref="Domain.Enums.GuildAccessLevel.Roster"/> access on
/// <see cref="GuildId"/>.
/// </summary>
public class GetRaidEventSummaryQuery : IQueryRequest<RaidEventSummaryResponse>
{
    /// <summary>Discord snowflake ID of the guild this event belongs to.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch this event belongs to.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>ID of the raid event to summarize.</summary>
    public required int EventId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user.</summary>
    public required string RequesterDiscordId { get; set; }
}
