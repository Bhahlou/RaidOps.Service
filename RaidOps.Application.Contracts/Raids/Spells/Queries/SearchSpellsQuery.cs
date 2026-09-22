using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Responses;

namespace RaidOps.Application.Contracts.Raids.Spells.Queries;

/// <summary>
/// Searches the seeded spell reference table by localized name substring — backs the spell picker
/// used when building a guild's attribution template. Officer-gated on <see cref="GuildId"/> since
/// it's only ever used from the (officer-only) template editor.
/// </summary>
public class SearchSpellsQuery : IQueryRequest<List<SpellResponse>>
{
    /// <summary>Discord snowflake ID of the guild the requester is asking on behalf of. Set by the controller, not from the request body.</summary>
    public required string GuildId { get; set; }

    /// <summary>Discord snowflake ID of the requesting user. Set by the controller, not from the request body.</summary>
    public required string RequesterDiscordId { get; set; }

    /// <summary>FK to the expansion to search spells on.</summary>
    public required int ExpansionId { get; set; }

    /// <summary>Substring to match against the spell's localized name.</summary>
    public required string SearchTerm { get; set; }

    /// <summary>The requester's active UI locale ("en", "fr", or "de") — picks which localized name column to search and return.</summary>
    public required string Locale { get; set; }
}
