using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Handles <see cref="LinkBnetAccountCommand"/> by inserting or updating the
/// <see cref="BattleNetAccount"/> record for the requesting user.
/// </summary>
public class LinkBnetAccountCommandHandler(
    IBnetAccountRepository bnetAccountRepository)
    : ICommandHandlerAsync<LinkBnetAccountCommand>
{
    /// <summary>
    /// Upserts the Battle.net account and returns a successful <see cref="CommandResponse"/>.
    /// This operation is idempotent — re-linking replaces the existing tokens.
    /// </summary>
    public async Task<Result<CommandResponse>> HandleAsync(
        LinkBnetAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        var account = new BattleNetAccount
        {
            UserDiscordId = command.UserDiscordId,
            BnetId = command.BnetId,
            BattleTag = command.BattleTag,
            AccessToken = command.AccessToken,
            RefreshToken = command.RefreshToken,
            TokenExpiry = command.TokenExpiry,
            Region = command.Region
        };

        await bnetAccountRepository.UpsertAsync(account, cancellationToken);
        return Result<CommandResponse>.Ok(new CommandResponse("BNet account linked successfully."));
    }
}
