using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Implementations.Characters;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

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
        CancellationToken cancellationToken)
    {
        var characters = await characterRepository.GetByUserWithDetailsAsync(
            query.UserDiscordId, activeOnly: true, cancellationToken);

        var dtos = characters.Select(CharacterMapper.ToDto);

        return Result<IEnumerable<CharacterDto>>.Ok(dtos);
    }
}
