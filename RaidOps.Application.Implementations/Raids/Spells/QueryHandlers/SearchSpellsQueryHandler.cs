using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Spells.QueryHandlers;

/// <summary>Handles <see cref="SearchSpellsQuery"/> — the one spell search backing every spell picker. Spells are public reference data, so there is no access check beyond being authenticated.</summary>
public class SearchSpellsQueryHandler(ISpellRepository spellRepository) : IQueryHandlerAsync<SearchSpellsQuery, List<SpellResponse>>
{
    private const int MaxResults = 20;

    /// <inheritdoc/>
    public async Task<Result<List<SpellResponse>>> HandleAsync(SearchSpellsQuery query, CancellationToken cancellationToken)
    {
        var spells = await spellRepository.SearchAsync(query.ExpansionId, query.SearchTerm, query.Locale, MaxResults, cancellationToken);

        var response = spells.Select(s => new SpellResponse
        {
            Id = s.SpellId,
            Name = LocalizedName(s, query.Locale),
            IconUrl = s.IconUrl,
        }).ToList();

        return Result<List<SpellResponse>>.Ok(response);
    }

    private static string LocalizedName(SpellAvailability spell, string locale) => locale switch
    {
        "fr" => spell.NameFr,
        "de" => spell.NameDe,
        _ => spell.NameEn,
    };
}
