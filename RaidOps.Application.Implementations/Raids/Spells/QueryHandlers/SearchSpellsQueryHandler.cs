using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Spells.QueryHandlers;

/// <summary>Handles <see cref="SearchSpellsQuery"/> — backs the spell picker used when building a guild's attribution template.</summary>
public class SearchSpellsQueryHandler(
    IGuildAccessService guildAccessService,
    ISpellRepository spellRepository) : IQueryHandlerAsync<SearchSpellsQuery, List<SpellResponse>>
{
    private const int MaxResults = 20;

    /// <inheritdoc/>
    public async Task<Result<List<SpellResponse>>> HandleAsync(SearchSpellsQuery query, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<SpellResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var spells = await spellRepository.SearchAsync(query.ExpansionId, query.SearchTerm, query.Locale, MaxResults, cancellationToken);

        var response = spells.Select(s => new SpellResponse
        {
            Id = s.Id,
            Name = LocalizedName(s, query.Locale),
            IconUrl = s.IconUrl,
        }).ToList();

        return Result<List<SpellResponse>>.Ok(response);
    }

    private static string LocalizedName(Spell spell, string locale) => locale switch
    {
        "fr" => spell.NameFr,
        "de" => spell.NameDe,
        _ => spell.NameEn,
    };
}
