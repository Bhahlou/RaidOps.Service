using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Implementations.Characters;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

/// <summary>
/// Returns the list of WoW characters imported by the requesting user,
/// including their class, race, realm, current level, and their linked Battle.net accounts —
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
        var bnetAccounts = await bnetAccountRepository.GetAllByDiscordIdAsync(query.UserDiscordId, cancellationToken);

        return Result<GetCharactersResponse>.Ok(new GetCharactersResponse
        {
            BnetAccounts = bnetAccounts.Select(account => new BnetAccountResponse
            {
                BnetId      = account.BnetId,
                BattleTag   = account.BattleTag,
                Region      = account.Region,
                TokenExpiry = account.TokenExpiry,
            }).ToList(),
            Characters = characters.Select(CharacterMapper.ToDto).ToList(),
        });
    }
}
