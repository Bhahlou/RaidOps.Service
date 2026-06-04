using RaidOps.Application.Contracts.Characters.Queries;
using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.QueryHandlers;

/// <summary>
/// Handles <see cref="GetBnetAccountQuery"/> by fetching the linked Battle.net account
/// for the requesting user.
/// </summary>
public class GetBnetAccountQueryHandler(IBnetAccountRepository bnetAccountRepository)
    : IQueryHandlerAsync<GetBnetAccountQuery, BnetAccountResponse>
{
    /// <summary>
    /// Returns the linked <see cref="BnetAccountResponse"/> for the user,
    /// or a failed result with error code <c>"NOT_FOUND"</c> if no account has been linked yet.
    /// </summary>
    public async Task<Result<BnetAccountResponse>> HandleAsync(
        GetBnetAccountQuery query,
        CancellationToken cancellationToken)
    {
        var account = await bnetAccountRepository.GetByDiscordIdAsync(query.UserDiscordId, cancellationToken);

        if (account is null)
            return Result<BnetAccountResponse>.Fail(ResponseDetail.NotFound);

        return Result<BnetAccountResponse>.Ok(new BnetAccountResponse
        {
            BnetId = account.BnetId,
            BattleTag = account.BattleTag,
            Region = account.Region,
            TokenExpiry = account.TokenExpiry
        });
    }
}
