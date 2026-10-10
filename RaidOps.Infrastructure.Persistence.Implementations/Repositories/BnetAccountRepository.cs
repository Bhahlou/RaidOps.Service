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
    /// Inserts or updates a Battle.net account linked to a user.
    /// Uses EF Core's change tracker: adds the entity if it doesn't exist for this
    /// (UserDiscordId, BnetId) pair, or updates its properties if it already does.
    /// </summary>
    public async Task UpsertAsync(BattleNetAccount account, CancellationToken cancellationToken = default)
    {
        var existing = await context.BattleNetAccounts
            .FindAsync([account.UserDiscordId, account.BnetId], cancellationToken);

        if (existing == null)
        {
            context.BattleNetAccounts.Add(account);
        }
        else
        {
            existing.BattleTag = account.BattleTag;
            existing.AccessToken = account.AccessToken;
            existing.RefreshToken = account.RefreshToken;
            existing.TokenExpiry = account.TokenExpiry;
            existing.Region = account.Region;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns all <see cref="BattleNetAccount"/>s linked to the user with the given Discord ID.
    /// </summary>
    public async Task<IReadOnlyList<BattleNetAccount>> GetAllByDiscordIdAsync(string discordId, CancellationToken cancellationToken = default)
        => await context.BattleNetAccounts
            .Where(a => a.UserDiscordId == discordId)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Deletes the Battle.net account matching the given user and BNet ID, if it exists.
    /// </summary>
    public async Task DeleteAsync(string discordId, string bnetId, CancellationToken cancellationToken = default)
    {
        var existing = await context.BattleNetAccounts
            .FindAsync([discordId, bnetId], cancellationToken);

        if (existing is null) return;

        context.BattleNetAccounts.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
    }
}
