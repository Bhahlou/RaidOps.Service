using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.QueryHandlers;

/// <summary>Handles <see cref="GetRaidEventAttributionsQuery"/> by merging the guild's attribution template with a raid event's existing fills and seated characters.</summary>
public class GetRaidEventAttributionsQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IGuildAttributionDefinitionsRepository definitionsRepository,
    IRaidEventAttributionsRepository attributionsRepository) : IQueryHandlerAsync<GetRaidEventAttributionsQuery, RaidEventAttributionsResponse>
{
    /// <inheritdoc/>
    public async Task<Result<RaidEventAttributionsResponse>> HandleAsync(GetRaidEventAttributionsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<RaidEventAttributionsResponse>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var raidEvent = await raidEventRepository.GetByIdAsync(query.EventId, query.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<RaidEventAttributionsResponse>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{query.EventId}' does not exist.");

        var visibleToRequester = raidEvent.PublicationStatus == RaidPublicationStatus.Published || raidEvent.SignupMode == SignupMode.Signup;
        if (accessLevel < GuildAccessLevel.Officer && !visibleToRequester)
            return Result<RaidEventAttributionsResponse>.Fail(ResponseDetail.Forbidden, "This raid event isn't published yet.");

        var definitions = await definitionsRepository.GetForGuildAsync(query.GuildId, cancellationToken);
        var fills = await attributionsRepository.GetForEventAsync(query.EventId, cancellationToken);

        var seatedAssignmentsById = raidEvent.Assignments
            .DistinctBy(a => a.CharacterId)
            .ToDictionary(a => a.CharacterId);

        var response = new RaidEventAttributionsResponse
        {
            Definitions = definitions.Select(d => new GuildAttributionDefinitionResponse
            {
                Id = d.Id,
                Label = d.Label,
                Section = d.Section,
                IsRepeatable = d.IsRepeatable,
                Cells = d.Cells.Select(AttributionCellMapper.ToResponse).ToList(),
                SortOrder = d.SortOrder,
            }).ToList(),

            Fills = fills
                .Where(f => seatedAssignmentsById.ContainsKey(f.CharacterId))
                .Select(f =>
                {
                    var character = seatedAssignmentsById[f.CharacterId].Character;
                    return new RaidEventAttributionFillResponse
                    {
                        DefinitionId = f.GuildAttributionDefinitionId,
                        CellId = f.AttributionDefinitionCellId,
                        InstanceIndex = f.InstanceIndex,
                        CharacterId = f.CharacterId,
                        CharacterName = character.Name,
                        ClassId = character.ClassId,
                    };
                }).ToList(),

            SeatedCharacters = seatedAssignmentsById.Values
                .Select(a => new SeatedCharacterResponse { CharacterId = a.Character.Id, Name = a.Character.Name, ClassId = a.Character.ClassId, SpecId = a.SpecId })
                .ToList(),
        };

        return Result<RaidEventAttributionsResponse>.Ok(response);
    }
}
