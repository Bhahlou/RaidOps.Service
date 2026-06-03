using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Reference;

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
    /// Returns the characters matching the given IDs that belong to the specified user,
    /// including realm, branch, and expansion states with their specs.
    /// </summary>
    Task<IEnumerable<Character>> GetByIdsWithDetailsAsync(IEnumerable<int> ids, string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the spec matching the given name and class, or <c>null</c> if not found.
    /// </summary>
    Task<Spec?> GetSpecByNameAndClassAsync(string name, int classId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the spec with the given Blizzard spec ID, or <c>null</c> if not found.
    /// </summary>
    Task<Spec?> GetSpecByIdAsync(int specId, CancellationToken cancellationToken = default);

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
    /// Inserts or updates the expansion state for a (character × expansion) pair,
    /// including guild name. If <paramref name="state"/> has specs, they fully replace
    /// any existing specs for that expansion state.
    /// </summary>
    Task UpsertExpansionStateAsync(CharacterExpansionState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the given character IDs as active in RaidOps for the specified user.
    /// Ignores IDs that do not belong to the user.
    /// </summary>
    Task ActivateAsync(IEnumerable<int> characterIds, string userDiscordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <c>IsActiveInRaidOps = false</c> for the character matching the given ID and owner.
    /// Returns <c>true</c> if the character was found and deactivated; <c>false</c> if not found.
    /// </summary>
    Task<bool> DeactivateAsync(int characterId, string userDiscordId, CancellationToken cancellationToken = default);
}
