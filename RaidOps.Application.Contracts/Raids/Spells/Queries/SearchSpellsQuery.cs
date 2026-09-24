using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Responses;

namespace RaidOps.Application.Contracts.Raids.Spells.Queries;

/// <summary>
/// Searches the spell reference data by localized name substring — backs the spell picker used when
/// building a guild branch's attribution template. Scoped to one guild branch: the expansion to search
/// (and therefore each spell's name/icon on it) is derived server-side from that branch. Officer-gated
/// on the branch since it's only ever used from the (officer-only) template editor.
/// </summary>
public class SearchSpellsQuery : IQueryRequest<List<SpellResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Surrogate ID of the guild branch whose expansion to search spells on. Set by the controller from the route.</summary>
    public required int GuildBranchId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>Substring to match against the spell's localized name.</summary>
    public required string SearchTerm { get; set; }

    /// <summary>The requester's active UI locale ("en", "fr", or "de") — picks which localized name column to search and return.</summary>
    public required string Locale { get; set; }
}
