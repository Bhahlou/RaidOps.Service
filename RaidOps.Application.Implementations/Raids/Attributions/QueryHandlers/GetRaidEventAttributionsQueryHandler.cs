using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.QueryHandlers;

/// <summary>Handles <see cref="GetRaidEventAttributionsQuery"/> by merging the event's guild branch's attribution template with the event's existing fills and seated characters.</summary>
public class GetRaidEventAttributionsQueryHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IRaidEventRepository raidEventRepository,
    IGuildAttributionDefinitionsRepository definitionsRepository,
    IRaidEventAttributionsRepository attributionsRepository,
    IRaidBossRepository raidBossRepository) : IQueryHandlerAsync<GetRaidEventAttributionsQuery, RaidEventAttributionsResponse>
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

        if (query.BossId != null)
        {
            var boss = await raidBossRepository.GetByIdAsync(query.BossId.Value, cancellationToken);
            if (boss == null || raidEvent.TargetZones.All(z => z.RaidZoneId != boss.RaidZoneId))
                return Result<RaidEventAttributionsResponse>.Fail(ResponseDetail.BossNotTargetedByEvent, $"Boss '{query.BossId}' is not targeted by this raid event.");
        }

        var expansionId = await guildBranchesRepository.GetCurrentExpansionIdAsync(query.GuildId, query.GuildBranchId, cancellationToken);
        if (expansionId is null)
            return Result<RaidEventAttributionsResponse>.Fail(ResponseDetail.GuildBranchNotFound, "Guild branch not found.");

        var definitions = await definitionsRepository.GetForBranchAsync(query.GuildId, query.GuildBranchId, query.BossId, cancellationToken);
        var fills = await attributionsRepository.GetForEventAsync(query.EventId, cancellationToken);

        var seatedAssignmentsById = raidEvent.Assignments
            .DistinctBy(a => a.CharacterId)
            .ToDictionary(a => a.CharacterId);

        var definitionIds = definitions.Select(d => d.Id).ToHashSet();

        var response = new RaidEventAttributionsResponse
        {
            Definitions = definitions.Select(d => AttributionCellMapper.ToDefinitionResponse(d, expansionId.Value)).ToList(),

            // Scoped to this page's own definitions — the event's other boss pages' fills are
            // irrelevant here and would only bloat the payload (cell IDs are globally unique
            // surrogate keys, so there's no cross-boss collision risk either way).
            Fills = fills
                .Where(f => definitionIds.Contains(f.GuildAttributionDefinitionId) && seatedAssignmentsById.ContainsKey(f.CharacterId))
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
