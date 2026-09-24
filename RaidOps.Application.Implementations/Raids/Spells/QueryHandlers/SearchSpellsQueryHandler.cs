using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Spells.QueryHandlers;

/// <summary>Handles <see cref="SearchSpellsQuery"/> — backs the spell picker used when building a guild branch's attribution template.</summary>
public class SearchSpellsQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    ISpellRepository spellRepository) : IQueryHandlerAsync<SearchSpellsQuery, List<SpellResponse>>
{
    private const int MaxResults = 20;

    /// <inheritdoc/>
    public async Task<Result<List<SpellResponse>>> HandleAsync(SearchSpellsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<SpellResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var expansionId = await guildBranchesRepository.GetCurrentExpansionIdAsync(query.GuildId, query.GuildBranchId, cancellationToken);
        if (expansionId is null)
            return Result<List<SpellResponse>>.Fail(ResponseDetail.GuildBranchNotFound, "Guild branch not found.");

        var spells = await spellRepository.SearchAsync(expansionId.Value, query.SearchTerm, query.Locale, MaxResults, cancellationToken);

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
