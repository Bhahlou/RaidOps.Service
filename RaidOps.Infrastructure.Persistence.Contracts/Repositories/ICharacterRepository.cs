using RaidOps.Domain.Models.Character;

namespace RaidOps.Infrastructure.Persistence.Contracts.Repositories;

/// <summary>Persistence for WoW characters and their per-expansion states.</summary>
public interface ICharacterRepository
{
    /// <summary>
    /// Returns characters owned by the given user, including realm, class, race, and expansion states.
    /// When <paramref name="activeOnly"/> is <c>true</c>, only returns characters with <c>IsActiveInRaidOps = true</c>.
    /// Ordered alphabetically by name.
    /// </summary>
    Task<IEnumerable<Character>> GetByUserWithDetailsAsync(string userDiscordId, bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of BNet character IDs already synced for the given user.
    /// </summary>
    Task<HashSet<long>> GetBnetIdsByUserAsync(string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the character identified by its (BnetCharacterId, RealmId) unique key.
    /// Does not override <c>IsActiveInRaidOps</c> on update — activation is handled separately.
    /// Returns the persisted entity (with its DB-generated <c>Id</c>).
    /// </summary>
    Task<Character> UpsertAsync(Character character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates the expansion state for a (character × expansion) pair.
    /// </summary>
    Task UpsertExpansionStateAsync(CharacterExpansionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the given character IDs as active in RaidOps for the specified user.
    /// Ignores IDs that do not belong to the user.
    /// </summary>
    Task ActivateAsync(IEnumerable<int> characterIds, string userDiscordId, CancellationToken cancellationToken = default);
}
