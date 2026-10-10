using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Responses;

namespace RaidOps.Application.Contracts.Raids.Spells.Queries;

/// <summary>
/// Searches the spell reference data of one expansion by localized name substring — backs every spell
/// picker (attribution template editor, raid buff admin screen). Spells are public reference data, so
/// the query carries no guild or requester: any authenticated user may search.
/// </summary>
public class SearchSpellsQuery : IQueryRequest<List<SpellResponse>>
{
    /// <summary>The expansion whose spells (and their name/icon on it) to search.</summary>
    public required int ExpansionId { get; set; }

    /// <summary>Substring to match against the spell's localized name.</summary>
    public required string SearchTerm { get; set; }

    /// <summary>The requester's active UI locale ("en", "fr", or "de") — picks which localized name column to search and return.</summary>
    public required string Locale { get; set; }
}
