using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Returns the list of WoW characters imported by the requesting user,
/// including their class, race, realm, and current level.
/// </summary>
public class GetCharactersQueryHandler(ICharacterRepository characterRepository)
    : IQueryHandlerAsync<GetCharactersQuery, IEnumerable<CharacterDto>>
{
    /// <inheritdoc />
    public async Task<Result<IEnumerable<CharacterDto>>> HandleAsync(
        GetCharactersQuery query,
        CancellationToken cancellationToken = default)
    {
        var characters = await characterRepository.GetByUserWithDetailsAsync(
            query.UserDiscordId, cancellationToken);

        var dtos = characters.Select(c =>
        {
            // Prefer the active expansion state; fall back to the one with the highest level.
            var activeState = c.ExpansionStates.FirstOrDefault(s => s.IsActive)
                           ?? c.ExpansionStates.OrderByDescending(s => s.Level).FirstOrDefault();

            return new CharacterDto
            {
                Id         = c.Id,
                Name       = c.Name,
                ClassId    = c.ClassId,
                ClassName  = c.Class.Name,
                ClassColor = "#" + c.Class.Color,
                RaceId     = c.RaceId,
                RaceName   = c.Race.Name,
                Faction    = c.Faction.ToString().ToUpperInvariant(),
                RealmName  = c.Realm.Name,
                RealmSlug  = c.Realm.Slug,
                Level      = activeState?.Level ?? 0,
                ItemLevel  = activeState?.ItemLevel,
            };
        });

        return Result<IEnumerable<CharacterDto>>.Ok(dtos);
    }
}
