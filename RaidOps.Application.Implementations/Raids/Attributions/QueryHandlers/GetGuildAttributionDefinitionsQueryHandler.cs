using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.QueryHandlers;

/// <summary>Handles <see cref="GetGuildAttributionDefinitionsQuery"/> by returning the guild's raid-attribution template.</summary>
public class GetGuildAttributionDefinitionsQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildAttributionDefinitionsRepository definitionsRepository)
    : IQueryHandlerAsync<GetGuildAttributionDefinitionsQuery, List<GuildAttributionDefinitionResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<GuildAttributionDefinitionResponse>>> HandleAsync(GetGuildAttributionDefinitionsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<GuildAttributionDefinitionResponse>>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var definitions = await definitionsRepository.GetForGuildAsync(query.GuildId, cancellationToken);

        var response = definitions.Select(d => new GuildAttributionDefinitionResponse
        {
            Id = d.Id,
            Label = d.Label,
            Section = d.Section,
            IsRepeatable = d.IsRepeatable,
            Cells = d.Cells.Select(AttributionCellMapper.ToResponse).ToList(),
            SortOrder = d.SortOrder,
        }).ToList();

        return Result<List<GuildAttributionDefinitionResponse>>.Ok(response);
    }
}
