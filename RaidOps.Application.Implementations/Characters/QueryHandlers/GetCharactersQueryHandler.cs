using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Implementations.Characters;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

/// <summary>
/// Returns the list of WoW characters imported by the requesting user,
/// including their class, race, realm, current level, and their linked Battle.net account —
/// everything the characters list page needs in a single request.
/// </summary>
public class GetCharactersQueryHandler(
    ICharacterRepository characterRepository,
    IBnetAccountRepository bnetAccountRepository)
    : IQueryHandlerAsync<GetCharactersQuery, GetCharactersResponse>
{
    /// <inheritdoc />
    public async Task<Result<GetCharactersResponse>> HandleAsync(
        GetCharactersQuery query,
        CancellationToken cancellationToken)
    {
        var characters = await characterRepository.GetByUserWithDetailsAsync(
            query.UserDiscordId, activeOnly: true, cancellationToken);
        var bnetAccount = await bnetAccountRepository.GetByDiscordIdAsync(query.UserDiscordId, cancellationToken);

        return Result<GetCharactersResponse>.Ok(new GetCharactersResponse
        {
            BnetAccount = bnetAccount is null ? null : new BnetAccountResponse
            {
                BnetId      = bnetAccount.BnetId,
                BattleTag   = bnetAccount.BattleTag,
                Region      = bnetAccount.Region,
                TokenExpiry = bnetAccount.TokenExpiry,
            },
            Characters = characters.Select(CharacterMapper.ToDto).ToList(),
        });
    }
}
