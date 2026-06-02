using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Handles <see cref="GetSyncedCharactersQuery"/> by returning all characters synced from BNet
/// for the requesting user, annotated with their <c>IsActiveInRaidOps</c> status.
/// </summary>
public class GetSyncedCharactersQueryHandler(ICharacterRepository characterRepository)
    : IQueryHandlerAsync<GetSyncedCharactersQuery, IEnumerable<SyncedCharacterDto>>
{
    /// <inheritdoc/>
    public async Task<Result<IEnumerable<SyncedCharacterDto>>> HandleAsync(
        GetSyncedCharactersQuery query,
        CancellationToken cancellationToken = default)
    {
        var characters = await characterRepository.GetByUserWithDetailsAsync(
            query.UserDiscordId, activeOnly: false, cancellationToken);

        var dtos = characters.Select(c =>
        {
            var activeState = c.ExpansionStates.FirstOrDefault(s => s.IsActive)
                           ?? c.ExpansionStates.OrderByDescending(s => s.Level).FirstOrDefault();

            return new SyncedCharacterDto
            {
                Id         = c.Id,
                Name       = c.Name,
                ClassId    = c.ClassId,
                ClassName  = c.Class.Name,
                ClassColor = "#" + c.Class.Color,
                RaceId     = c.RaceId,
                RaceName   = c.Race.Name,
                Faction    = c.Faction.ToString().ToUpperInvariant(),
                BranchName = c.Branch.Name,
                RealmName  = c.Realm.Name,
                Level      = activeState?.Level ?? 0,
                IsActive   = c.IsActiveInRaidOps
            };
        });

        return Result<IEnumerable<SyncedCharacterDto>>.Ok(dtos);
    }
}
