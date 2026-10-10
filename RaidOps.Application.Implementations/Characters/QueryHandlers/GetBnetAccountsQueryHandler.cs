using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

/// <summary>
/// Handles <see cref="GetBnetAccountsQuery"/> by fetching all Battle.net accounts
/// linked to the requesting user.
/// </summary>
public class GetBnetAccountsQueryHandler(IBnetAccountRepository bnetAccountRepository)
    : IQueryHandlerAsync<GetBnetAccountsQuery, List<BnetAccountResponse>>
{
    /// <summary>
    /// Returns the linked <see cref="BnetAccountResponse"/>s for the user, or an empty list
    /// if none have been linked yet.
    /// </summary>
    public async Task<Result<List<BnetAccountResponse>>> HandleAsync(
        GetBnetAccountsQuery query,
        CancellationToken cancellationToken)
    {
        var accounts = await bnetAccountRepository.GetAllByDiscordIdAsync(query.UserDiscordId, cancellationToken);

        return Result<List<BnetAccountResponse>>.Ok(accounts.Select(account => new BnetAccountResponse
        {
            BnetId = account.BnetId,
            BattleTag = account.BattleTag,
            Region = account.Region,
            TokenExpiry = account.TokenExpiry
        }).ToList());
    }
}
