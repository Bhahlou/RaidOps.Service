using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IBnetAccountRepository"/>.
/// </summary>
public class BnetAccountRepository(RaidOpsDbContext context) : IBnetAccountRepository
{
    /// <summary>
    /// Inserts or updates the Battle.net account linked to a user.
    /// Uses EF Core's change tracker: adds the entity if it doesn't exist,
    /// or updates its properties if it already does.
    /// </summary>
    public async Task UpsertAsync(BattleNetAccount account, CancellationToken cancellationToken = default)
    {
        var existing = await context.BattleNetAccounts
            .FindAsync([account.UserDiscordId], cancellationToken);

        if (existing == null)
        {
            context.BattleNetAccounts.Add(account);
        }
        else
        {
            existing.BnetId = account.BnetId;
            existing.BattleTag = account.BattleTag;
            existing.AccessToken = account.AccessToken;
            existing.RefreshToken = account.RefreshToken;
            existing.TokenExpiry = account.TokenExpiry;
            existing.Region = account.Region;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the <see cref="BattleNetAccount"/> linked to the user with the given Discord ID,
    /// or <c>null</c> if the user has not yet linked a BNet account.
    /// </summary>
    public async Task<BattleNetAccount?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken = default)
        => await context.BattleNetAccounts
            .FirstOrDefaultAsync(a => a.UserDiscordId == discordId, cancellationToken);
}
